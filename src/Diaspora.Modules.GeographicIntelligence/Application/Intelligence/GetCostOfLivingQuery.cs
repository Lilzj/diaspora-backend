using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Intelligence;

/// <summary>What it costs to live in a neighbourhood.</summary>
[Authorize(Policy = GeographicIntelligencePolicies.IntelligenceRead)]
public sealed record GetCostOfLivingQuery : IQuery<CostOfLivingResponse>
{
    public required string IdOrSlug { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Serves the cost figures this platform can stand behind, and refuses to manufacture the rest.
/// <para>
/// <b>Housing, from Diaspora's own listings (17 Sep 2026).</b> The user decided cost of living is
/// computed from what members list rather than bought in: the median monthly asking rent of the
/// homes filed in the neighbourhood, as an index against the median of the country's, in the
/// country's most-listed currency. It is published only when both samples reach the configured
/// minimums, and it says how many homes are behind it.
/// </para>
/// <para>
/// <b>The other five categories stay empty, and so does <c>overallIndex</c>.</b> Food, transport,
/// utilities, healthcare and leisure have no source. An overall figure computed from housing alone
/// would be a housing index under a broader name, so none is published; a client shows the housing
/// row and says the rest is not measured.
/// </para>
/// <para>
/// The moderated band (B11) is still served beside it, with its own provenance. It is a different
/// claim — a contributor's assessment — and the two are never blended.
/// </para>
/// </summary>
public sealed class GetCostOfLivingQueryHandler
    : IQueryHandler<GetCostOfLivingQuery, CostOfLivingResponse>
{
    public const string ListingsSource = "Diaspora listings";

    private const string ProfileSource = "Diaspora neighbourhood profiles";

    private readonly NeighborhoodResolver _resolver;
    private readonly INeighborhoodDirectory _profiles;
    private readonly IRentStatistics _rents;
    private readonly ICountryRepository _countries;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;

    public GetCostOfLivingQueryHandler(
        NeighborhoodResolver resolver,
        INeighborhoodDirectory profiles,
        IRentStatistics rents,
        ICountryRepository countries,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock)
    {
        _resolver = resolver;
        _profiles = profiles;
        _rents = rents;
        _countries = countries;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<CostOfLivingResponse>> Handle(
        GetCostOfLivingQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "cost-of-living"));

        var area = await _resolver.ResolveAsync(
            request.IdOrSlug, request.CountryCode, cancellationToken);

        if (area is null)
        {
            return Result.Failure<CostOfLivingResponse>(
                GeographicIntelligenceErrors.NeighborhoodNotFound);
        }

        var now = _clock.GetUtcNow();
        var options = _options.Value;

        var profile = await _profiles.GetAsync(area.Id.Value, cancellationToken);
        var bandName = profile?.CostOfLivingBand;
        var band = BandScale.ParseBand(bandName);

        var rents = await _rents.CompareMonthlyRentAsync(
            area.CountryCode.Value, area.Path.Value, cancellationToken);

        var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);
        var countryName = country?.Name.Value ?? area.CountryCode.Value;

        var indexed = rents.Currency is not null
            && rents.AreaMedianMonthlyRent is > 0m
            && rents.CountryMedianMonthlyRent is > 0m
            && rents.AreaSampleSize >= options.MinimumRentListingsInArea
            && rents.CountrySampleSize >= options.MinimumRentListingsInCountry;

        var response = new CostOfLivingResponse
        {
            NeighborhoodId = area.Slug.Value,
            AreaId = area.Id.Value,
            Name = area.Name.Value,

            // True when at least one category is published — housing, today, where the samples
            // allow. The band beside it is real either way.
            IsAvailable = indexed,

            Band = band is null ? null : bandName,
            BandScore = band is null ? null : BandScale.ToScore(band.Value),

            // Never housing under a broader name. See the class notes.
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

            Freshness = indexed
                ? RentFreshness(rents, area.Name.Value, countryName, now, options)
                : Unindexed(rents, band is not null, bandName, profile, area.Name.Value, now, options)
        };

        return Result.Success(response);
    }

    private static DataFreshnessResponse RentFreshness(
        RentComparison rents,
        string areaName,
        string countryName,
        DateTimeOffset now,
        GeographicIntelligenceOptions options) =>
        new()
        {
            ComputedAt = now,
            SourceName = ListingsSource,
            Methodology = string.Format(
                CultureInfo.InvariantCulture,
                "The median monthly asking rent of the {0} homes to rent listed on Diaspora in {1}, as "
                + "an index against the median of the {2} listed across {3} (100 = the same). Weekly, "
                + "quarterly and yearly rents are converted to monthly.",
                rents.AreaSampleSize,
                areaName,
                rents.CountrySampleSize,
                countryName),
            Limitations = RentLimitations(rents),
            ValidUntil = now.AddHours(options.FreshnessWindowHours),
            SampleSize = rents.AreaSampleSize
        };

    private static DataFreshnessResponse Unindexed(
        RentComparison rents,
        bool hasBand,
        string? bandName,
        NeighborhoodSummary? profile,
        string areaName,
        DateTimeOffset now,
        GeographicIntelligenceOptions options)
    {
        var assessedBy = profile?.IsApproved == true
            ? "a Diaspora moderator"
            : "a Diaspora contributor, pending moderator approval";

        var why = string.Format(
            CultureInfo.InvariantCulture,
            "No rent index is published for {0}: it has {1} {2} to rent listed on Diaspora and its "
            + "country has {3}; an index needs at least {4} here and {5} in the country.",
            areaName,
            rents.AreaSampleSize,
            rents.AreaSampleSize == 1 ? "home" : "homes",
            rents.CountrySampleSize,
            options.MinimumRentListingsInArea,
            options.MinimumRentListingsInCountry);

        return new DataFreshnessResponse
        {
            ComputedAt = hasBand ? profile?.LastReviewedAt ?? now : now,
            SourceName = hasBand ? ProfileSource : ListingsSource,
            Methodology = hasBand ? $"{BandScale.Methodology(bandName!, assessedBy)} {why}" : why,
            Limitations = RentLimitations(rents),
            ValidUntil = now.AddHours(options.FreshnessWindowHours),
            SampleSize = rents.AreaSampleSize
        };
    }

    private static List<string> RentLimitations(RentComparison rents)
    {
        var limitations = new List<string>
        {
            "Asking rents of homes listed on Diaspora, not agreed rents and not a survey of the "
            + "neighbourhood.",
            "Housing only. Food, transport, utilities, healthcare and leisure are not measured, "
            + "so there is no overall cost-of-living figure.",
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

        return limitations;
    }
}
