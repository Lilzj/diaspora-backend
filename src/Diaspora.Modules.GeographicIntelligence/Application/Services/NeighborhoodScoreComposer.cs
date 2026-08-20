using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Pagination;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Services;

/// <summary>
/// Composes a neighbourhood's score breakdown out of facts two other modules already hold.
/// <para>
/// <b>This class is the phase.</b> The frontend register's finding was that "the certified route
/// returns three bare numbers with no source, no date and no methodology". Wave 4's finding was
/// sharper and is the one this acts on: those three numbers are <em>null on every row and always
/// have been</em>, while B11 holds nine moderated liveability bands with an author, an approver and
/// a review date — and the map surfaces read the empty one. Nothing here computes a new fact. It
/// reads the module that owns each one and publishes it with the provenance §26 requires.
/// </para>
/// <para>
/// <b>Four rules decide what gets emitted, and they are the whole of §54.</b>
/// </para>
/// <para>
/// 1. <b>A dimension appears only when something real produced it.</b> There is no placeholder row
/// and no zero standing in for an absence. Everything else appears in
/// <c>unavailableDimensions</c> with a reason, which is a fact the client has been inferring.
/// </para>
/// <para>
/// 2. <b>An assessed band is encoded, never estimated.</b> The band travels beside the number and
/// the methodology says the number carries no precision the band does not — see
/// <see cref="BandScale"/>.
/// </para>
/// <para>
/// 3. <b>A count is ranked, never normalised against an invented ceiling.</b> Every ceiling that is
/// not measured is a fiction: pick 50 and a busy district pins at 100, pick 500 and every real
/// place reads as empty. A percentile needs no ceiling and its methodology can name its population.
/// </para>
/// <para>
/// 4. <b>A count with no cheap comparison population is published as a count.</b> Jobs, events and
/// services are on <c>resourceCounts</c> — real, and exactly what B4 already serves — and withheld
/// from <c>dimensions</c> with <see cref="DimensionUnavailableReason.NoComparableScale"/>. The
/// figure is not hidden; only the score is, because the score is the part that would be invented.
/// </para>
/// </summary>
public sealed class NeighborhoodScoreComposer
{
    /// <summary>
    /// Where a ranked figure says it came from.
    /// <para>
    /// The platform's own published content, and the name says so rather than implying a survey.
    /// </para>
    /// </summary>
    private const string PlatformSource = "Diaspora platform content";

    /// <summary>Where an assessed band says it came from. Moderated editorial, not a measurement.</summary>
    private const string ProfileSource = "Diaspora neighbourhood profiles";

    /// <summary>
    /// The comparison population, bounded.
    /// <para>
    /// B11's directory caps <c>FindAsync</c> at fifty and that ceiling is taken rather than fought:
    /// a percentile over fifty places is a stable statistic, and asking for every neighbourhood in
    /// a country would turn one screen into an unbounded read.
    /// </para>
    /// <para>
    /// <b>The set it returns is narrower than "every profiled neighbourhood" and the methodology
    /// says so.</b> It is the most active profiled neighbourhoods <em>with at least one live
    /// housing listing</em>. That selection is B11's and is right for its own callers; taking it
    /// here means a place with no listings is ranked against places that have them, which is a
    /// defensible comparison and a misleading one if left unstated. It is stated, in
    /// <see cref="PercentileRank.Methodology"/>, on every ranked figure.
    /// </para>
    /// </summary>
    private const int ComparisonPopulationLimit = 50;

    private readonly NeighborhoodResolver _resolver;
    private readonly IAdministrativeAreaRepository _areas;
    private readonly INeighborhoodDirectory _profiles;
    private readonly ResourceCountAggregator _counts;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;

    public NeighborhoodScoreComposer(
        NeighborhoodResolver resolver,
        IAdministrativeAreaRepository areas,
        INeighborhoodDirectory profiles,
        ResourceCountAggregator counts,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock)
    {
        _resolver = resolver;
        _areas = areas;
        _profiles = profiles;
        _counts = counts;
        _options = options;
        _clock = clock;
    }

    /// <summary>Resolves and composes, or null when no such neighbourhood exists.</summary>
    public async Task<NeighborhoodScoreResponse?> ComposeAsync(
        string? idOrSlug, string? countryCode, CancellationToken cancellationToken = default)
    {
        var area = await _resolver.ResolveAsync(idOrSlug, countryCode, cancellationToken);

        return area is null ? null : await ComposeAsync(area, cancellationToken);
    }

    /// <summary>
    /// Composes for an area already resolved.
    /// <para>
    /// Separated so the comparison endpoint can resolve several areas and compose each without
    /// re-resolving — the alternative being a comparison of three neighbourhoods that costs six
    /// catalogue reads.
    /// </para>
    /// </summary>
    public async Task<NeighborhoodScoreResponse> ComposeAsync(
        AdministrativeArea area, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(area);

        var now = _clock.GetUtcNow();
        var settings = _options.Value;

        var profile = await _profiles.GetAsync(area.Id.Value, cancellationToken);

        var counts = await _counts.CountInAreaAsync(area.Id, area.Path, cancellationToken);

        var population = await _profiles.FindAsync(
            area.CountryCode.Value,
            areaPathPrefix: null,
            ComparisonPopulationLimit,
            cancellationToken);

        var dimensions = new List<ScoreDimensionResponse>();
        var unavailable = new List<UnavailableDimensionResponse>();

        AddAssessed(
            dimensions, unavailable,
            ScoreDimensionId.Transport, "Transport & walkability",
            profile?.Walkability, profile, now, settings,
            "Walkability is a judgement about getting around on foot. It is not a public-transport "
            + "score and says nothing about frequency, cost or accessibility of services.");

        AddAssessed(
            dimensions, unavailable,
            ScoreDimensionId.CostOfLiving, "Cost of living",
            profile?.CostOfLivingBand, profile, now, settings,
            "A single relative band for the whole neighbourhood. It is not an index, carries no "
            + "currency, and does not break down by housing, food or transport.");

        AddRanked(
            dimensions, unavailable,
            ScoreDimensionId.Housing, "Housing availability",
            profile?.ListingCount,
            [.. population.Select(entry => entry.ListingCount)],
            "listings currently advertised", "homes listed",
            area.CountryCode.Value, now, settings,
            "Counts what is advertised on Diaspora, not what exists. A neighbourhood where homes "
            + "change hands privately will read as quiet.");

        AddRanked(
            dimensions, unavailable,
            ScoreDimensionId.CommunityActivity, "Community activity",
            profile?.InsightCount,
            [.. population.Select(entry => entry.InsightCount)],
            "published community insights", "community insights",
            area.CountryCode.Value, now, settings,
            "Counts moderated contributions members have published about this place. It measures "
            + "how much has been written, not how active the community is.");

        // Real counts, no publishable scale. The figure is on `resourceCounts` either way.
        AddCountOnly(unavailable, ScoreDimensionId.Jobs, "Jobs", counts, "jobs");
        AddCountOnly(unavailable, ScoreDimensionId.Events, "Events", counts, "events");
        AddCountOnly(unavailable, ScoreDimensionId.Services, "Services", counts, "providers");

        // The two with no source anywhere on the platform, and no vendor contracted for either.
        AddNoSource(
            unavailable, ScoreDimensionId.Safety, "Safety",
            "Diaspora holds no safety data. No source is contracted, and a community-assessed "
            + "safety rating is deliberately not one of the bands a neighbourhood profile records.");

        AddNoSource(
            unavailable, ScoreDimensionId.Amenities, "Amenities",
            "Diaspora holds no inventory of shops, parks, schools or clinics, and none is "
            + "contracted.");

        var nearby = await NearbyAsync(area, cancellationToken);

        return new NeighborhoodScoreResponse
        {
            NeighborhoodId = area.Slug.Value,
            AreaId = area.Id.Value,
            Name = area.Name.Value,
            CityName = profile?.CityName,
            CountryCode = area.CountryCode.Value,
            Latitude = area.Center?.Latitude,
            Longitude = area.Center?.Longitude,
            Overall = Blend(dimensions),
            Dimensions = dimensions,
            UnavailableDimensions = unavailable,
            ResourceCounts = counts,
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = PlatformSource,
                Methodology =
                    "Composed on request from Diaspora's own records: moderated neighbourhood "
                    + "profiles for assessed bands, and counts of published content for ranked "
                    + "dimensions. No third-party data is used and nothing is estimated.",
                Limitations =
                [
                    "Every figure describes activity on Diaspora, not the neighbourhood itself.",
                    "Dimensions with no source are listed in unavailableDimensions rather than "
                    + "estimated."
                ],
                ValidUntil = now.AddHours(settings.FreshnessWindowHours),
                SampleSize = dimensions.Count
            },
            NearbyNeighborhoodIds = nearby
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Dimension builders
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A moderated band, encoded. Withheld when nobody has assessed it.</summary>
    private static void AddAssessed(
        List<ScoreDimensionResponse> dimensions,
        List<UnavailableDimensionResponse> unavailable,
        string id,
        string label,
        string? bandName,
        NeighborhoodSummary? profile,
        DateTimeOffset now,
        GeographicIntelligenceOptions settings,
        string limitation)
    {
        var band = BandScale.ParseBand(bandName);

        if (band is null || profile is null)
        {
            Withhold(
                unavailable, id, label, DimensionUnavailableReason.NotAssessed,
                profile is null
                    ? "Nobody has published a profile for this neighbourhood yet."
                    : "Nobody has assessed this for this neighbourhood yet.");

            return;
        }

        var assessedBy = profile.IsApproved
            ? "a Diaspora moderator"
            : "a Diaspora contributor, pending moderator approval";

        // The review date when there is one — the profile says when it was last looked at, and
        // that is a truer computedAt than the instant this request happened to run.
        var computedAt = profile.LastReviewedAt ?? now;

        dimensions.Add(new ScoreDimensionResponse
        {
            Id = id,
            Label = label,
            Score = BandScale.ToScore(band.Value),
            Band = bandName,
            Explanation = $"Assessed as '{bandName}'.",
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = computedAt,
                SourceName = ProfileSource,
                Methodology = BandScale.Methodology(bandName!, assessedBy),
                Limitations = [limitation],
                ValidUntil = computedAt.AddHours(settings.FreshnessWindowHours),

                // One assessment. Stated plainly, because a client that shows n=1 beside a band is
                // telling a member exactly how much weight it deserves.
                SampleSize = 1
            }
        });

        GeographicIntelligenceTelemetry.DimensionsServed.Add(
            1, new KeyValuePair<string, object?>("dimension", id));
    }

    /// <summary>A count, ranked against a real population. Withheld when the population is too thin.</summary>
    private static void AddRanked(
        List<ScoreDimensionResponse> dimensions,
        List<UnavailableDimensionResponse> unavailable,
        string id,
        string label,
        int? value,
        IReadOnlyCollection<int> population,
        string what,
        string noun,
        string countryCode,
        DateTimeOffset now,
        GeographicIntelligenceOptions settings,
        string limitation)
    {
        if (value is null)
        {
            Withhold(
                unavailable, id, label, DimensionUnavailableReason.NotAssessed,
                "Nobody has published a profile for this neighbourhood yet.");

            return;
        }

        var rank = PercentileRank.Of(value.Value, population, settings.MinimumComparisonPopulation);

        if (rank is null)
        {
            Withhold(
                unavailable, id, label, DimensionUnavailableReason.InsufficientPopulation,
                $"Too few neighbourhoods in {countryCode} have a published profile to rank "
                + "against. The underlying count is in resourceCounts.");

            return;
        }

        dimensions.Add(new ScoreDimensionResponse
        {
            Id = id,
            Label = label,
            Score = rank.Value,
            Explanation = PercentileRank.Explanation(value.Value, noun, population.Count),
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = PlatformSource,
                Methodology = PercentileRank.Methodology(what, population.Count, countryCode),
                Limitations = [limitation],
                ValidUntil = now.AddHours(settings.FreshnessWindowHours),
                SampleSize = population.Count
            }
        });

        GeographicIntelligenceTelemetry.DimensionsServed.Add(
            1, new KeyValuePair<string, object?>("dimension", id));
    }

    /// <summary>
    /// A vertical that reported a real count and has no scale to express it on.
    /// <para>
    /// The distinction <c>ResourceCountAggregator</c> draws is preserved exactly: an absent key
    /// means the vertical could not be asked, a present zero means it was asked and there is
    /// nothing there. Those are different sentences and only one of them is about the place.
    /// </para>
    /// </summary>
    private static void AddCountOnly(
        List<UnavailableDimensionResponse> unavailable,
        string id,
        string label,
        IReadOnlyDictionary<string, int> counts,
        string resourceType)
    {
        if (!counts.TryGetValue(resourceType, out var count))
        {
            Withhold(
                unavailable, id, label, DimensionUnavailableReason.SourceUnavailable,
                "This vertical could not be reached, so no figure is published. That is not the "
                + "same as there being none here.");

            return;
        }

        Withhold(
            unavailable, id, label, DimensionUnavailableReason.NoComparableScale,
            $"{count} in this neighbourhood — the count is in resourceCounts. Diaspora has no "
            + "scale on which to publish that as a 0–100 score without inventing one.");
    }

    private static void AddNoSource(
        List<UnavailableDimensionResponse> unavailable, string id, string label, string explanation) =>
        Withhold(unavailable, id, label, DimensionUnavailableReason.NoDataSource, explanation);

    private static void Withhold(
        List<UnavailableDimensionResponse> unavailable,
        string id,
        string label,
        DimensionUnavailableReason reason,
        string explanation)
    {
        unavailable.Add(new UnavailableDimensionResponse
        {
            Id = id,
            Label = label,
            Reason = reason.ToString(),
            Explanation = explanation
        });

        GeographicIntelligenceTelemetry.DimensionsUnavailable.Add(
            1,
            new KeyValuePair<string, object?>("dimension", id),
            new KeyValuePair<string, object?>("reason", reason.ToString()));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Composition
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The overall figure, from the emitted dimensions and nothing else.
    /// <para>
    /// <b>Null rather than zero when nothing has a source.</b> A neighbourhood nobody has assessed
    /// must not read as a terrible one — B11 makes the same point about <c>Unknown</c> not being a
    /// low band, and an overall of 0 would undo it at the last step.
    /// </para>
    /// <para>
    /// Unweighted, deliberately. A weighting is a claim about what matters when choosing where to
    /// live, and this platform is not in a position to make that claim on a member's behalf. An
    /// equal mean of what is known is the one blend that asserts nothing extra.
    /// </para>
    /// </summary>
    private static SourcedValueResponse<int>? Blend(List<ScoreDimensionResponse> dimensions) =>
        dimensions.Count == 0
            ? null
            : new SourcedValueResponse<int>
            {
                Value = (int)Math.Round(
                    dimensions.Average(dimension => dimension.Score), MidpointRounding.AwayFromZero),

                // `backend`, and it is earned: every input is a figure some module owns. The
                // client marks its own blend `simulated` today because that one contains invented
                // inputs, and its type is what forced the distinction into the open.
                Source = "backend"
            };

    /// <summary>
    /// Sibling neighbourhoods, for the comparison surface.
    /// <para>
    /// B4's children of the same parent — the places somebody choosing between neighbourhoods is
    /// actually choosing between. Slugs rather than identifiers because that is what the client's
    /// comparison route takes today.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>> NearbyAsync(
        AdministrativeArea area, CancellationToken cancellationToken)
    {
        if (area.ParentId is null)
        {
            return [];
        }

        var siblings = await _areas.GetChildrenAsync(
            area.CountryId,
            area.ParentId,
            AdministrativeLevel.Neighborhood,
            new PaginationRequest { Page = 1, PageSize = 20 },
            cancellationToken);

        return
        [
            .. siblings.Items
                .Where(sibling => sibling.Id != area.Id)
                .Select(sibling => sibling.Slug.Value)
        ];
    }
}
