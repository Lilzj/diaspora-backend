using System.Globalization;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.GeographicIntelligence.Application.Intelligence;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Services;

/// <summary>A point a member asked about, as the client sent it.</summary>
public sealed record AreaPoint
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>The country the point is in. Required: see <see cref="GeographicIntelligenceErrors.AreaCountryRequired"/>.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The Google place name the member chose, passed back unchanged. Null for "around me".</summary>
    public string? Name { get; init; }
}

/// <summary>
/// Composes "the area around a point" (Phase 2A of retiring the place catalogue, 14 Oct 2026).
/// <para>
/// <b>Everything is computed from rows' own coordinates.</b> Counts come from every vertical's
/// <see cref="ICircleResourceCounter"/>; the rent index from the homes within the circle against
/// the country's (<see cref="IRentStatistics.CompareMonthlyRentWithinAsync"/>); safety from
/// corroborated community reports within the circle and, where data.police.uk covers the country,
/// the crimes it recorded within a mile of the point. No catalogue node is read, so a point in a
/// country the catalogue never held is described exactly like one in a country it did.
/// </para>
/// <para>
/// <b>No score is invented.</b> The assessed bands a neighbourhood score blended were written by an
/// editor about a named neighbourhood, and the ranked ones compared a neighbourhood with its peers;
/// a circle has neither, so every dimension is listed as unavailable with the reason and the real
/// figures travel as counts.
/// </para>
/// </summary>
public sealed class AreaReportComposer
{
    private const string PlatformSource = "Diaspora platform content";

    /// <summary>The longest place name echoed back. A Google place name is far shorter.</summary>
    private const int MaximumNameLength = 200;

    /// <summary>How long one vertical may take to count before it is reported unavailable.</summary>
    private static readonly TimeSpan CounterTimeout = TimeSpan.FromSeconds(3);

    private readonly IEnumerable<ICircleResourceCounter> _counters;
    private readonly IRentStatistics _rents;
    private readonly ICommunitySafetyReports _reports;
    private readonly IPoliceCrimeData _police;
    private readonly ICountryRepository _countries;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<AreaReportComposer> _logger;

    public AreaReportComposer(
        IEnumerable<ICircleResourceCounter> counters,
        IRentStatistics rents,
        ICommunitySafetyReports reports,
        IPoliceCrimeData police,
        ICountryRepository countries,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock,
        ILogger<AreaReportComposer> logger)
    {
        _counters = counters;
        _rents = rents;
        _reports = reports;
        _police = police;
        _countries = countries;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// The radius a request describes: the default when none is given, refused outside
    /// (0, maximum].
    /// </summary>
    public Result<double> ResolveRadius(double? radiusKm)
    {
        var settings = _options.Value;
        var radius = radiusKm ?? settings.DefaultAreaRadiusKm;

        if (double.IsNaN(radius) || double.IsInfinity(radius))
        {
            return Result.Failure<double>(GeographicIntelligenceErrors.CoordinateNotFinite("radiusKm"));
        }

        if (radius <= 0d || radius > settings.MaximumAreaRadiusKm)
        {
            return Result.Failure<double>(GeographicIntelligenceErrors.CoordinateOutOfRange(
                "radiusKm", radius, 0d, settings.MaximumAreaRadiusKm));
        }

        return Result.Success(radius);
    }

    /// <summary>Validates the point and composes its report.</summary>
    public async Task<Result<AreaReportResponse>> ComposeAsync(
        AreaPoint point, double radiusKm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(point);

        var validated = Validate(point);

        if (validated.IsFailure)
        {
            GeographicIntelligenceTelemetry.InvalidCoordinates.Add(1);

            return Result.Failure<AreaReportResponse>(validated.Error);
        }

        var countryCode = validated.Value;
        var settings = _options.Value;
        var now = _clock.GetUtcNow();

        var country = await _countries.GetByCodeAsync(countryCode, cancellationToken);
        var countryName = country?.Name.Value ?? countryCode.Value;

        var name = Clean(point.Name);
        var displayName = name ?? string.Format(
            CultureInfo.InvariantCulture, "Within {0} of you", Kilometres(radiusKm));

        // "Within 2 km of Westlands" / "Within 2 km of here" — what each count's method sentence says.
        var circleWords = string.Format(
            CultureInfo.InvariantCulture,
            "within {0} of {1}",
            Kilometres(radiusKm),
            name ?? "this point");

        var circle = CircleArea.Around(point.Latitude, point.Longitude, radiusKm);

        var (counts, lowerBound, unavailable) = await CountAsync(circle, cancellationToken);

        var costOfLiving = await CostOfLivingAsync(
            countryCode.Value, countryName, point, radiusKm, circleWords, now, settings, cancellationToken);

        var safety = await SafetyAsync(
            countryCode.Value, countryName, point, radiusKm, name, circleWords, now, settings, cancellationToken);

        return Result.Success(new AreaReportResponse
        {
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            RadiusKm = radiusKm,
            CountryCode = countryCode.Value,
            CountryName = countryName,
            Name = name,
            DisplayName = displayName,
            ResourceCounts = counts,
            LowerBoundResourceTypes = lowerBound,
            UnavailableResourceTypes = unavailable,
            Overall = null,
            Dimensions = [],
            UnavailableDimensions = UnavailableDimensions(counts, unavailable),
            CostOfLiving = costOfLiving,
            Safety = safety,
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = PlatformSource,
                Methodology = string.Format(
                    CultureInfo.InvariantCulture,
                    "Composed on request from what is published on Diaspora {0}, measured from each "
                    + "listing's, event's and post's own coordinates. Rows with no coordinates are not "
                    + "counted.",
                    circleWords),
                Limitations =
                [
                    "Every figure describes what is published on Diaspora, not the area itself.",
                    "Homes are placed at the approximate point their listing shows, not their address, "
                    + "so a home near the edge of the circle may be counted on the wrong side of it."
                ],
                ValidUntil = now.AddHours(settings.FreshnessWindowHours),
                SampleSize = counts.Values.Sum()
            }
        });
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Validation
    // ════════════════════════════════════════════════════════════════════════════════════════

    private static Result<CountryCode> Validate(AreaPoint point)
    {
        if (double.IsNaN(point.Latitude) || double.IsInfinity(point.Latitude))
        {
            return Result.Failure<CountryCode>(GeographicIntelligenceErrors.CoordinateNotFinite("latitude"));
        }

        if (double.IsNaN(point.Longitude) || double.IsInfinity(point.Longitude))
        {
            return Result.Failure<CountryCode>(GeographicIntelligenceErrors.CoordinateNotFinite("longitude"));
        }

        if (point.Latitude is < -90d or > 90d)
        {
            return Result.Failure<CountryCode>(
                GeographicIntelligenceErrors.CoordinateOutOfRange("latitude", point.Latitude, -90d, 90d));
        }

        if (point.Longitude is < -180d or > 180d)
        {
            return Result.Failure<CountryCode>(
                GeographicIntelligenceErrors.CoordinateOutOfRange("longitude", point.Longitude, -180d, 180d));
        }

        if (string.IsNullOrWhiteSpace(point.CountryCode)
            || CountryCode.TryCreate(point.CountryCode.Trim()) is not { IsSuccess: true } country)
        {
            return Result.Failure<CountryCode>(GeographicIntelligenceErrors.AreaCountryRequired);
        }

        return Result.Success(country.Value);
    }

    private static string? Clean(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaximumNameLength ? trimmed[..MaximumNameLength] : trimmed;
    }

    /// <summary>"2 km", "2.5 km", "0.5 km".</summary>
    public static string Kilometres(double radiusKm) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.##} km", radiusKm);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Counts
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Every vertical's count within the circle, in turn.
    /// <para>
    /// Sequential for the reason Location's aggregators are: every counter reaches the one scoped
    /// <c>DbContext</c>, which refuses a second operation while the first is running. A counter that
    /// throws or overruns loses its own key and is named as unavailable — never a zero.
    /// </para>
    /// </summary>
    private async Task<(Dictionary<string, int> Counts, List<string> LowerBound, List<string> Unavailable)> CountAsync(
        CircleArea circle, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowerBound = new List<string>();
        var unavailable = new List<string>();

        foreach (var counter in _counters.OrderBy(counter => counter.ResourceType, StringComparer.Ordinal))
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(CounterTimeout);

            try
            {
                var count = await counter.CountWithinAsync(circle, budget.Token);

                counts[counter.ResourceType] = count.Count;

                if (count.IsLowerBound)
                {
                    lowerBound.Add(counter.ResourceType);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Circle counter '{ResourceType}' exceeded its {Timeout}ms budget.",
                    counter.ResourceType, CounterTimeout.TotalMilliseconds);

                unavailable.Add(counter.ResourceType);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Circle counter '{ResourceType}' failed.", counter.ResourceType);

                unavailable.Add(counter.ResourceType);
            }
        }

        return (counts, lowerBound, unavailable);
    }

    /// <summary>
    /// Why the circle has no score on each dimension. The counts it does have are on
    /// <c>resourceCounts</c>.
    /// </summary>
    private static List<UnavailableDimensionResponse> UnavailableDimensions(
        IReadOnlyDictionary<string, int> counts, IReadOnlyCollection<string> unavailable)
    {
        const string NotAssessed =
            "Assessed bands belong to neighbourhood profiles an editor wrote about a named place. An "
            + "area around a point has none, and borrowing one would describe somewhere else.";

        var dimensions = new List<UnavailableDimensionResponse>
        {
            Unavailable(ScoreDimensionId.Transport, "Transport & walkability", DimensionUnavailableReason.NotAssessed, NotAssessed),
            Unavailable(
                ScoreDimensionId.CostOfLiving, "Cost of living", DimensionUnavailableReason.NoComparableScale,
                "The rent within the circle against the country's is on costOfLiving, as an index with its "
                + "sample size. There is no 0–100 score for it."),
            Counted(ScoreDimensionId.Housing, "Housing availability", counts, unavailable, "housing"),
            Unavailable(
                ScoreDimensionId.CommunityActivity, "Community activity", DimensionUnavailableReason.NoComparableScale,
                "Community updates within the circle are counted on resourceCounts. There is no population "
                + "of circles to rank the count against."),
            Counted(ScoreDimensionId.Jobs, "Jobs", counts, unavailable, "jobs"),
            Counted(ScoreDimensionId.Events, "Events", counts, unavailable, "events"),
            Unavailable(
                ScoreDimensionId.Services, "Services", DimensionUnavailableReason.NoComparableScale,
                "Service providers are found by the places they serve, not counted around a point."),
            Unavailable(
                ScoreDimensionId.Safety, "Safety", DimensionUnavailableReason.NoDataSource,
                "Diaspora does not publish a safety score. Sourced counts are on safety."),
            Unavailable(
                ScoreDimensionId.Amenities, "Amenities", DimensionUnavailableReason.NoDataSource,
                "Diaspora holds no inventory of shops, parks, schools or clinics, and none is contracted.")
        };

        foreach (var dimension in dimensions)
        {
            GeographicIntelligenceTelemetry.DimensionsUnavailable.Add(
                1,
                new KeyValuePair<string, object?>("dimension", dimension.Id),
                new KeyValuePair<string, object?>("reason", dimension.Reason));
        }

        return dimensions;
    }

    private static UnavailableDimensionResponse Counted(
        string id,
        string label,
        IReadOnlyDictionary<string, int> counts,
        IReadOnlyCollection<string> unavailable,
        string resourceType) =>
        counts.TryGetValue(resourceType, out var count)
            ? Unavailable(
                id, label, DimensionUnavailableReason.NoComparableScale,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} within the circle — the count is in resourceCounts. Diaspora has no scale on "
                    + "which to publish that as a 0–100 score without inventing one.",
                    count))
            : Unavailable(
                id, label, DimensionUnavailableReason.SourceUnavailable,
                unavailable.Contains(resourceType)
                    ? "This vertical could not be reached, so no figure is published. That is not the "
                      + "same as there being none here."
                    : "This deployment does not count this vertical around a point.");

    private static UnavailableDimensionResponse Unavailable(
        string id, string label, DimensionUnavailableReason reason, string explanation) =>
        new()
        {
            Id = id,
            Label = label,
            Reason = reason.ToString(),
            Explanation = explanation
        };

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Cost of living
    // ════════════════════════════════════════════════════════════════════════════════════════

    private async Task<AreaCostOfLivingResponse> CostOfLivingAsync(
        string countryCode,
        string countryName,
        AreaPoint point,
        double radiusKm,
        string circleWords,
        DateTimeOffset now,
        GeographicIntelligenceOptions settings,
        CancellationToken cancellationToken)
    {
        var rents = await _rents.CompareMonthlyRentWithinAsync(
            countryCode, point.Latitude, point.Longitude, radiusKm, cancellationToken);

        // The same minimums as a neighbourhood's, so a thin circle says "not enough data" rather
        // than publishing the median of two homes.
        var indexed = rents.Currency is not null
            && rents.AreaMedianMonthlyRent is > 0m
            && rents.CountryMedianMonthlyRent is > 0m
            && rents.AreaSampleSize >= settings.MinimumRentListingsInArea
            && rents.CountrySampleSize >= settings.MinimumRentListingsInCountry;

        var limitations = new List<string>
        {
            "Asking rents of homes listed on Diaspora, not agreed rents and not a survey of the area.",
            "Housing only. Food, transport, utilities, healthcare and leisure are not measured, so there "
            + "is no overall cost-of-living figure.",
            "A median of few listings moves a lot when one home is added or let."
        };

        if (rents.OtherCurrencyListings > 0)
        {
            limitations.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} priced in a currency other than {2} and left out rather than converted.",
                rents.OtherCurrencyListings,
                rents.OtherCurrencyListings == 1 ? "listing is" : "listings are",
                rents.Currency));
        }

        var freshness = new DataFreshnessResponse
        {
            ComputedAt = now,
            SourceName = GetCostOfLivingQueryHandler.ListingsSource,
            Methodology = indexed
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "The median monthly asking rent of the {0} homes to rent listed on Diaspora {1}, as an "
                    + "index against the median of the {2} listed across {3} (100 = the same). Weekly, "
                    + "quarterly and yearly rents are converted to monthly.",
                    rents.AreaSampleSize, circleWords, rents.CountrySampleSize, countryName)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Not enough data: {0} {1} to rent {2} listed on Diaspora {3}, and {4} across {5}. An "
                    + "index needs at least {6} in the circle and {7} in the country.",
                    rents.AreaSampleSize,
                    rents.AreaSampleSize == 1 ? "home" : "homes",
                    rents.AreaSampleSize == 1 ? "is" : "are",
                    circleWords,
                    rents.CountrySampleSize,
                    countryName,
                    settings.MinimumRentListingsInArea,
                    settings.MinimumRentListingsInCountry),
            Limitations = limitations,
            ValidUntil = now.AddHours(settings.FreshnessWindowHours),
            SampleSize = rents.AreaSampleSize
        };

        return new AreaCostOfLivingResponse
        {
            IsAvailable = indexed,
            UnavailableReason = indexed ? null : "InsufficientSample",
            SampleSize = rents.AreaSampleSize,
            CountrySampleSize = rents.CountrySampleSize,
            OverallIndex = null,
            BaselineName = indexed
                ? $"the median asking rent of homes listed on Diaspora in {countryName}"
                : null,
            Currency = indexed ? rents.Currency : null,
            Categories = indexed
                ?
                [
                    new CostOfLivingCategoryResponse
                    {
                        Id = "housing",
                        Label = "Rent",
                        Index = (int)Math.Round(
                            100m * rents.AreaMedianMonthlyRent!.Value / rents.CountryMedianMonthlyRent!.Value,
                            MidpointRounding.AwayFromZero),
                        TypicalMonthlyAmount = Math.Round(
                            rents.AreaMedianMonthlyRent.Value, 0, MidpointRounding.AwayFromZero),
                        TypicalMonthlyCurrency = rents.Currency
                    }
                ]
                : [],
            Freshness = freshness
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    private async Task<AreaSafetyResponse> SafetyAsync(
        string countryCode,
        string countryName,
        AreaPoint point,
        double radiusKm,
        string? name,
        string circleWords,
        DateTimeOffset now,
        GeographicIntelligenceOptions settings,
        CancellationToken cancellationToken)
    {
        GeographicIntelligenceTelemetry.DimensionsUnavailable.Add(
            1,
            new KeyValuePair<string, object?>("dimension", ScoreDimensionId.Safety),
            new KeyValuePair<string, object?>("reason", DimensionUnavailableReason.NoDataSource.ToString()));

        var window = TimeSpan.FromDays(settings.SafetyReportWindowDays);
        var reports = await _reports.CountCorroboratedWithinAsync(
            point.Latitude, point.Longitude, radiusKm, now, window, cancellationToken);

        var community = new SafetyCountResponse
        {
            Kind = "community-reports",
            Source = GetSafetyScoreQueryHandler.CommunitySource,
            IsAvailable = true,
            From = reports.From,
            To = reports.To,
            Area = string.Format(CultureInfo.InvariantCulture, "Alerts pinned {0}", circleWords),
            Method = string.Format(
                CultureInfo.InvariantCulture,
                "Police and Emergency alerts members published {0} over the last {1} days that other "
                + "members corroborated enough for Diaspora to mark them community-verified, counted at "
                + "each alert's own pin. Alerts their authors withdrew, and anything moderators removed, "
                + "are not counted.{2}",
                circleWords,
                settings.SafetyReportWindowDays,
                reports.IsLowerBound ? " The count stopped at its reading limit, so there are at least this many." : string.Empty),
            Total = reports.Total,
            Categories =
            [
                .. reports.ByCategory
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new SafetyCountCategoryResponse
                    {
                        Id = entry.Key,
                        Label = entry.Key,
                        Count = entry.Value
                    })
            ]
        };

        SafetyCountResponse police;

        if (_police.Covers(countryCode))
        {
            // data.police.uk answers for its own fixed one-mile radius, whatever circle the member
            // chose, and the area sentence says so.
            var area = string.Format(
                CultureInfo.InvariantCulture, "Within one mile of {0}", name ?? "this point");

            const string Method =
                "Street-level crimes recorded by the police within one mile of the point, counted over the "
                + "most recent months data.police.uk has published. Each crime is placed at an anonymised "
                + "point near where it happened.";

            var answer = await _police.CountAsync(point.Latitude, point.Longitude, cancellationToken);

            police = GetSafetyScoreQueryHandler.FromPoliceAnswer(answer, area, Method);
        }
        else
        {
            // Not available here — which is not a low figure. Never a score.
            police = GetSafetyScoreQueryHandler.PoliceNotPublished(countryName);
        }

        return new AreaSafetyResponse
        {
            IsAvailable = false,
            Score = null,
            Band = null,
            UnavailableReason = DimensionUnavailableReason.NoDataSource.ToString(),
            Counts = [community, police],
            Limitations =
            [
                "Diaspora does not publish a safety score. It publishes what named sources counted, "
                + "separately, and none of those counts measures how safe a place is.",
                "Community reports are alerts members filed and other members corroborated. Many incidents "
                + "are never reported on Diaspora, and a quiet place on Diaspora may simply have few members.",
                "Police-recorded crime covers England, Wales and Northern Ireland only. Everywhere else has "
                + "no official source Diaspora may republish, and says so rather than showing nothing.",
                "Neither says anything about a particular street, a time of day, or what it is like to live "
                + "there, and neither should be the only basis for a decision about where to live."
            ]
        };
    }
}
