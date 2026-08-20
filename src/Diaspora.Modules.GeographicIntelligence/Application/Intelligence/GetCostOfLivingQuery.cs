using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Scores;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Housing.Integration;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Intelligence;

/// <summary>What it costs to live in a neighbourhood.</summary>
public sealed record GetCostOfLivingQuery : IQuery<CostOfLivingResponse>
{
    public required string IdOrSlug { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Serves the one real cost figure this platform holds, and refuses to manufacture the other five.
/// <para>
/// <b>The gap the frontend registered is real and only a fifth of it is closable.</b> B11 records
/// cost of living as one moderated ordinal band per neighbourhood — that is real, dated, attributed
/// data and it is served here with its provenance. The client's surface wants six category indices
/// against a national baseline in a stated currency, and five of those have no source anywhere.
/// </para>
/// <para>
/// So the response carries the band and leaves <c>overallIndex</c>, <c>baselineName</c>,
/// <c>currency</c> and <c>categories</c> empty with <c>isAvailable: false</c>. Publishing an index
/// derived from the band would be the fabrication §31 and §54 both name: "118" is a claim about how
/// much more expensive somewhere is, to the percent, against a baseline nobody measured.
/// </para>
/// </summary>
public sealed class GetCostOfLivingQueryHandler
    : IQueryHandler<GetCostOfLivingQuery, CostOfLivingResponse>
{
    private const string ProfileSource = "Diaspora neighbourhood profiles";

    private readonly NeighborhoodResolver _resolver;
    private readonly INeighborhoodDirectory _profiles;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;

    public GetCostOfLivingQueryHandler(
        NeighborhoodResolver resolver,
        INeighborhoodDirectory profiles,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock)
    {
        _resolver = resolver;
        _profiles = profiles;
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
        var window = _options.Value.FreshnessWindowHours;

        var profile = await _profiles.GetAsync(area.Id.Value, cancellationToken);
        var bandName = profile?.CostOfLivingBand;
        var band = BandScale.ParseBand(bandName);

        var computedAt = profile?.LastReviewedAt ?? now;

        var assessedBy = profile?.IsApproved == true
            ? "a Diaspora moderator"
            : "a Diaspora contributor, pending moderator approval";

        return Result.Success(new CostOfLivingResponse
        {
            NeighborhoodId = area.Slug.Value,
            AreaId = area.Id.Value,
            Name = area.Name.Value,

            // False on every deployment. `isAvailable` describes the *index*, which is what the
            // surface is for; the band beside it is real whether or not the index ever arrives.
            IsAvailable = false,

            Band = band is null ? null : bandName,
            BandScore = band is null ? null : BandScale.ToScore(band.Value),

            OverallIndex = null,
            BaselineName = null,
            Currency = null,
            Categories = [],

            Freshness = new DataFreshnessResponse
            {
                ComputedAt = computedAt,
                SourceName = ProfileSource,
                Methodology = band is null
                    ? "Diaspora holds no cost-of-living data for this neighbourhood."
                    : BandScale.Methodology(bandName!, assessedBy),
                Limitations =
                [
                    "Diaspora publishes a single relative band per neighbourhood and no index.",
                    "There is no category breakdown, no currency and no national baseline, because "
                    + "no cost-of-living data source is contracted.",
                    "The band compares neighbourhoods within Diaspora's own coverage, not against "
                    + "any external measure."
                ],
                ValidUntil = computedAt.AddHours(window),
                SampleSize = band is null ? null : 1
            }
        });
    }
}
