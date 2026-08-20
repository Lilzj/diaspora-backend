using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// The one class in B11 that talks to Guides.
/// <para>
/// <b>The quietly most valuable integration in the module.</b> "Renting in Mauritius", "Avoiding
/// housing scams", "Understanding rental agreements" — a member about to send a deposit to a
/// stranger in a country they have not arrived in yet is exactly the reader those guides were
/// written for, and the listing page is the one screen where they will actually be read.
/// </para>
/// <para>
/// <b>B8 has no country-scoped guide listing, and B11 does not invent one.</b> B10 closed by
/// recording that two verticals had already wanted that capability and had each declined to reach
/// into another module's read model to get it. This is the third, and it declines for the same
/// reason: querying B8's listings from here would put a Housing query on a Guides table, and the
/// first schema change in B8 would break a module B8 has never heard of.
/// </para>
/// <para>
/// <b>What it does instead is resolve identifiers an operator configured.</b>
/// <c>HousingOptions.ContextualGuides</c> maps a country code to the guides that belong beside a
/// listing there, and this class checks each still exists through B8's published directory before
/// attaching it. That is deployment configuration rather than a cross-module query, and it means a
/// retracted guide silently stops appearing.
/// </para>
/// <para>
/// <b>Selection is never by anything the landlord chose.</b> A landlord picking which safety guides
/// appear beside their own listing is the arrangement a fraudster would design.
/// </para>
/// <para>
/// The dependency is optional: a deployment without Guides simply shows no explainers.
/// </para>
/// </summary>
public sealed class HousingGuideResolver
{
    /// <summary>
    /// How many guides one listing carries. Four: enough for the scam warning, the tenancy
    /// explainer, the local renting guide and the moving checklist, and few enough that they are
    /// read rather than scrolled past.
    /// </summary>
    private const int MaxGuides = 4;

    /// <summary>
    /// The configuration key for guides that apply everywhere — the scam warning, the deposit
    /// explainer. Falls back to these when a country has no list of its own.
    /// </summary>
    public const string GlobalKey = "*";

    private readonly IGuideDirectory? _guides;
    private readonly IOptions<HousingOptions> _options;
    private readonly ILogger<HousingGuideResolver> _logger;

    public HousingGuideResolver(
        IOptions<HousingOptions> options,
        ILogger<HousingGuideResolver> logger,
        IGuideDirectory? guides = null)
    {
        _options = options;
        _logger = logger;
        _guides = guides;
    }

    /// <summary>Whether this deployment has a guide directory at all.</summary>
    public bool IsAvailable => _guides is not null;

    /// <summary>
    /// The guides worth showing beside a property.
    /// <para>
    /// Returns empty rather than failing on any problem. A missing explainer is a slightly poorer
    /// listing page; a refused publication because Guides was briefly unavailable is a landlord who
    /// cannot let their property.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<HousingGuideReference>> ResolveForAsync(
        HousingListing listing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return await ResolveAsync(
            ConfiguredFor(listing.Location.CountryCode), cancellationToken);
    }

    /// <summary>
    /// Resolves an explicit set of guide identifiers, dropping any B8 no longer publishes.
    /// </summary>
    public async Task<IReadOnlyList<HousingGuideReference>> ResolveAsync(
        IReadOnlyList<Guid> guideIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        if (_guides is null || guideIds.Count == 0)
        {
            return [];
        }

        var keys = guideIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(MaxGuides)
            .Select(GuideId.From)
            .ToList();

        if (keys.Count == 0)
        {
            return [];
        }

        try
        {
            var found = await _guides.FindManyAsync(keys, cancellationToken);

            // Ordered by the configured order rather than by whatever the dictionary returned:
            // the scam warning is first for a reason.
            return
            [
                .. keys
                    .Select((key, index) => (Key: key, Index: index))
                    .Where(entry => found.ContainsKey(entry.Key.Value))
                    .Select(entry =>
                    {
                        var guide = found[entry.Key.Value];

                        return HousingGuideReference.Create(
                            guide.GuideId, guide.Title, guide.Slug, entry.Index);
                    })
            ];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Guides outage must not refuse a publication. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Resolving contextual guides failed. The listing has none attached.");

            return [];
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// The guides an operator configured for a country, falling back to the global list.
    /// <para>
    /// The two are concatenated rather than one replacing the other: "avoiding housing scams"
    /// applies everywhere and should not be lost because a deployment added a Mauritius list.
    /// </para>
    /// </summary>
    private IReadOnlyList<Guid> ConfiguredFor(string countryCode)
    {
        var configured = _options.Value.ContextualGuides;

        if (configured.Count == 0)
        {
            return [];
        }

        // TryGetValue rather than GetValueOrDefault: the option is an IDictionary rather than an
        // IReadOnlyDictionary so the configuration binder can populate it, and the extension does
        // not apply to the mutable interface.
        var global = configured.TryGetValue(GlobalKey, out var globalGuides)
            ? globalGuides
            : [];

        var country = configured.TryGetValue(countryCode, out var countryGuides)
            ? countryGuides
            : [];

        return [.. global.Concat(country).Distinct()];
    }
}
