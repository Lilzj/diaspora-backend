using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Feed;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Recommendations.Infrastructure.Feed;

/// <summary>
/// Assembles the home feed from whatever sections modules contributed.
/// <para>
/// <b>Sections run sequentially rather than concurrently, and that is a deliberate choice against
/// the obvious optimisation.</b> Each section is a full engine pass, and every pass shares one
/// <c>DbContext</c> — EF Core's is not thread-safe, and a concurrent fan-out across one would fail
/// intermittently under load in a way that reproduces on nobody's machine. The sequential version is
/// slower and correct; making it faster is a matter of scoping a context per section, which is a
/// change worth making with a measurement rather than in advance of one.
/// </para>
/// <para>
/// <b>A section that throws is dropped, not fatal.</b> A home screen that fails entirely because the
/// events pool was unavailable is worse in every way than one missing a row, and a member cannot act
/// on either failure.
/// </para>
/// <para>
/// <b>Sections below their minimum are dropped rather than shown short.</b> A carousel with one card
/// looks broken; an absent section looks deliberate. That is a product judgement and it belongs to
/// the contributing module, which is why the minimum is on the definition.
/// </para>
/// </summary>
public sealed class DefaultFeedComposer : IFeedComposer
{
    private readonly IEnumerable<IFeedSectionContributor> _contributors;
    private readonly IRecommendationEngine _engine;
    private readonly ILogger<DefaultFeedComposer> _logger;

    public DefaultFeedComposer(
        IEnumerable<IFeedSectionContributor> contributors,
        IRecommendationEngine engine,
        ILogger<DefaultFeedComposer> logger)
    {
        _contributors = contributors;
        _engine = engine;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ComposedFeedSection>> ComposeAsync(
        PersonalizationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definitions = ResolveSections(context);
        var composed = new List<ComposedFeedSection>(definitions.Count);

        foreach (var definition in definitions)
        {
            var section = await FillAsync(definition, context, cancellationToken);

            if (section is not null)
            {
                composed.Add(section);
            }
        }

        return composed;
    }

    /// <summary>
    /// Which sections this member gets, in display order.
    /// <para>
    /// Two filters, and both are the member's own doing: a section that needs personalization is
    /// omitted for somebody who opted out, and a section that needs history is omitted for somebody
    /// who has none. Neither is degraded — both are absent, because a "because you viewed" row for
    /// somebody who has viewed nothing is a lie with a heading.
    /// </para>
    /// </summary>
    private List<FeedSectionDefinition> ResolveSections(PersonalizationContext context)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sections = new List<FeedSectionDefinition>();

        foreach (var contributor in _contributors)
        {
            foreach (var definition in contributor.GetSections())
            {
                if (!seen.Add(definition.Key))
                {
                    _logger.LogWarning(
                        "Two modules contributed a feed section keyed '{Key}'. The later one was "
                        + "ignored; the first wins.",
                        definition.Key);

                    continue;
                }

                if (definition.RequiresPersonalization && !context.IsPersonalizationAllowed)
                {
                    continue;
                }

                if (!definition.ShowOnColdStart && context.IsColdStart)
                {
                    continue;
                }

                sections.Add(definition);
            }
        }

        return sections
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Key, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<ComposedFeedSection?> FillAsync(
        FeedSectionDefinition definition,
        PersonalizationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _engine.GenerateAsync(
                new RecommendationRequest
                {
                    UserId = context.UserId,
                    Surface = definition.Surface,
                    TargetType = definition.TargetType,
                    Limit = definition.Limit,
                    AccessTier = context.AccessTier,

                    // The home feed's rows are not individually persisted. A member scrolling one
                    // screen would otherwise generate eight stored sets, and every acceptance rate
                    // in the analytics would be divided by eight.
                    Transient = true
                },
                cancellationToken);

            return result.Items.Count < definition.MinimumItems
                ? null
                : new ComposedFeedSection
                {
                    Definition = definition,
                    Items = result.Items,
                    IsColdStart = result.IsColdStart
                };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Feed section '{Key}' failed and was dropped from the feed.",
                definition.Key);

            return null;
        }
    }
}
