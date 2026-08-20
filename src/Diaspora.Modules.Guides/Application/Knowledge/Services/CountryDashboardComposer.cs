using Diaspora.Modules.Guides.Application.Knowledge.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Assembles a country dashboard: seven shelves, the navigation breakdown, what people are
/// searching for, and what else the platform holds for the country.
/// <para>
/// <b>The reusable surface the brief asks every module to consume rather than rebuild.</b> Jobs
/// wanting "new to this country? start here", Housing wanting neighbourhood guidance beside a
/// listing, Community Life wanting culture guides beside a festival — every one of them was going to
/// write a version of this, and every version would have had its own idea of what "official" means.
/// </para>
/// <para>
/// <b>The shelves are short and the identifiers are resolved once.</b> Seven shelves of six guides
/// each overlap heavily — a featured guide is usually also a popular one — so the identifiers are
/// collected across every shelf and resolved in a single batched call. Resolving per shelf would be
/// seven queries returning largely the same rows.
/// </para>
/// <para>
/// <b>Shelves are queried, not curated, except the featured one.</b> That is the point of the split:
/// featured is what a person chose, and everything else is what the platform observed. A dashboard
/// that curated all seven would be an editorial job nobody has time to do for every country.
/// </para>
/// </summary>
public sealed class CountryDashboardComposer
{
    private readonly KnowledgeGuideFinder _finder;
    private readonly KnowledgeCardResolver _cards;
    private readonly KnowledgeDirectoryComposer _composer;
    private readonly KnowledgeStatisticsBuilder _statistics;
    private readonly ICountryKnowledgeAnalyticsRepository _analytics;
    private readonly IKnowledgeSearchTermRepository _searchTerms;
    private readonly ILearningPathRepository _paths;
    private readonly CountryContextContributorRegistry _context;

    /// <summary>
    /// B8.2's band.
    /// <para>
    /// A direct dependency rather than another contributed context block, and the distinction is
    /// worth stating. <c>ICountryContextContributor</c> exists so a module this one cannot reference
    /// can put a count and a route on the dashboard; country intelligence is in <em>this</em>
    /// assembly, owned by the same module, and it contributes content rather than a count — the
    /// emergency numbers are rendered here, not counted. Routing it through a seam built for foreign
    /// modules would be indirection bought with nothing.
    /// </para>
    /// </summary>
    private readonly Intelligence.Services.CountryIntelligenceComposer _intelligence;

    private readonly IOptions<KnowledgeOptions> _options;
    private readonly IClock _clock;

    public CountryDashboardComposer(
        KnowledgeGuideFinder finder,
        KnowledgeCardResolver cards,
        KnowledgeDirectoryComposer composer,
        KnowledgeStatisticsBuilder statistics,
        ICountryKnowledgeAnalyticsRepository analytics,
        IKnowledgeSearchTermRepository searchTerms,
        ILearningPathRepository paths,
        CountryContextContributorRegistry context,
        Intelligence.Services.CountryIntelligenceComposer intelligence,
        IOptions<KnowledgeOptions> options,
        IClock clock)
    {
        _intelligence = intelligence;
        _finder = finder;
        _cards = cards;
        _composer = composer;
        _statistics = statistics;
        _analytics = analytics;
        _searchTerms = searchTerms;
        _paths = paths;
        _context = context;
        _options = options;
        _clock = clock;
    }

    public async Task<CountryDashboardResponse> ComposeAsync(
        CountryKnowledgeDirectory directory,
        string? locale,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var country = directory.CountryCode;
        var shelfSize = KnowledgeLimits.DashboardShelfSize;

        var featuredIds = directory.FeaturedFor(null).Take(shelfSize).ToList();

        var recentlyUpdated = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.RecentlyUpdated, null, cancellationToken);

        var popular = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.Popular, null, cancellationToken);

        var official = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.MostHelpful,
            filter => filter with { OfficialOnly = true }, cancellationToken);

        // Community is everything nobody verified. Expressed as "not verified" rather than as its
        // own flag, because that is what it means — and a flag would have to be maintained by
        // whatever changed the verification, in a module that does not own this shelf.
        var community = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.MostHelpful,
            filter => filter with { VerifiedOnly = false }, cancellationToken);

        var beginner = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.MostHelpful,
            filter => filter with { Difficulty = GuideDifficulty.Beginner }, cancellationToken);

        var advanced = await ShelfAsync(
            country, language, shelfSize, GuideSortOrder.MostHelpful,
            filter => filter with { Difficulty = GuideDifficulty.Advanced }, cancellationToken);

        // Every identifier on the page, resolved once. Shelves overlap heavily and this is the
        // difference between one query and seven.
        var cards = await _cards.ResolveAsync(
            [
                .. featuredIds,
                .. recentlyUpdated,
                .. popular,
                .. official,
                .. community,
                .. beginner,
                .. advanced
            ],
            cancellationToken);

        var usage = await _analytics.GetForReadAsync(country, cancellationToken);

        var (displayName, summary) = directory.Localized(locale);

        return new CountryDashboardResponse
        {
            CountryCode = country,
            DisplayName = displayName,
            Summary = summary,
            PrimaryLanguage = directory.PrimaryLanguage,
            Languages = directory.Languages,
            Statistics = await _statistics.BuildAsync(country, directory, cancellationToken),

            Featured = KnowledgeCardResolver.Order(featuredIds, cards),
            RecentlyUpdated = KnowledgeCardResolver.Order(recentlyUpdated, cards),
            Popular = KnowledgeCardResolver.Order(popular, cards),
            Official = KnowledgeCardResolver.Order(official, cards),
            Community = KnowledgeCardResolver.Order(community, cards),
            Beginner = KnowledgeCardResolver.Order(beginner, cards),
            Advanced = KnowledgeCardResolver.Order(advanced, cards),

            LearningPaths =
            [
                .. (await _paths.ListByCountryAsync(country, publishedOnly: true, cancellationToken))
                    .Select(LearningPathMapper.ToSummary)
            ],

            CategoryBreakdown = await _composer.ComposeBreakdownAsync(
                directory, ViewsOf(usage), locale, cancellationToken),

            TrendingTopics = await TrendingAsync(country, cancellationToken),

            Context =
            [
                .. (await _context.CollectAsync(country, cancellationToken))
                    .Select(block => new CountryContextBlockResponse
                    {
                        Kind = block.Kind,
                        Label = block.Label,
                        Count = block.Count,
                        Route = block.Route
                    })
            ],

            // B8.2's band. One property rather than ten, and a route to the full surface — which is
            // how country intelligence reaches this dashboard without there being two of them.
            Intelligence = await _intelligence.ComposeSummaryAsync(
                country, locale, cancellationToken)
        };
    }

    /// <summary>
    /// One shelf, as identifiers.
    /// <para>
    /// The mutator lets each shelf narrow the shared filter without any of them assembling the
    /// audience fields by hand — a shelf that forgot the staff flag would show a hidden guide on a
    /// public dashboard.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ShelfAsync(
        string countryCode,
        string? language,
        int limit,
        GuideSortOrder sort,
        Func<GuideQueryFilter, GuideQueryFilter>? narrow,
        CancellationToken cancellationToken)
    {
        var filter = _finder.Filter(
            countryCode, guideCategoryKeys: null, language, sort, limit);

        if (narrow is not null)
        {
            filter = narrow(filter);
        }

        return await _finder.FindIdsAsync(filter, cancellationToken);
    }

    /// <summary>
    /// What people are searching for in this country, within the configured window.
    /// <para>
    /// Derived from searches rather than from tags, deliberately: a list built from what has been
    /// written tells you what the platform is good at, and one built from what people asked for
    /// tells you what they need. The two diverge most sharply exactly when something has changed,
    /// which is when a knowledge base most needs to notice.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<TrendingTopicResponse>> TrendingAsync(
        string countryCode, CancellationToken cancellationToken)
    {
        var window = _clock.UtcNow.AddDays(-Math.Max(1, _options.Value.TrendingWindowDays));

        var terms = await _searchTerms.TopAsync(
            countryCode, KnowledgeLimits.MaxTrendingTopics, window, cancellationToken);

        return
        [
            .. terms.Select(term => new TrendingTopicResponse
            {
                Term = term.Term,
                SearchCount = term.SearchCount,
                ResultCount = term.ResultCount,
                IsUnanswered = term.IsUnanswered,
                LastSearchedAt = term.LastSearchedAt
            })
        ];
    }

    private static Dictionary<string, long> ViewsOf(CountryKnowledgeAnalytics? usage) =>
        usage is null
            ? new Dictionary<string, long>(StringComparer.Ordinal)
            : usage.CategoryBreakdown.ToDictionary(
                view => view.KnowledgeCategoryKey, view => view.Views, StringComparer.Ordinal);
}
