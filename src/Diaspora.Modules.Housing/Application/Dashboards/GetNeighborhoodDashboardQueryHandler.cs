using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Dashboards;

/// <summary>
/// Assembles the editorial view of the neighbourhood estate.
/// <para>
/// <b>Reads one bounded page and computes over it, rather than counting the whole estate.</b> An
/// editorial team does not act on the total; they act on the twenty places that most need writing
/// about, and a screen that scanned every profile to produce an exact "1,284" would be slower and
/// no more useful. <c>TotalProfiles</c> is the count of the sample, and it says so.
/// </para>
/// <para>
/// <b>The two lists are the point.</b> "Most active" is where members actually are; "least
/// complete" is where the platform has least to tell them. A place that is in both is where an
/// hour of somebody's writing buys the most.
/// </para>
/// </summary>
public sealed class GetNeighborhoodDashboardQueryHandler
    : IQueryHandler<GetNeighborhoodDashboardQuery, NeighborhoodDashboardResponse>
{
    /// <summary>
    /// Wide enough that the two lists are drawn from a real estate rather than a page, and capped
    /// so an editorial screen never becomes a scan.
    /// </summary>
    private const int SampleSize = 100;

    private const int ListLength = 10;

    private readonly INeighborhoodProfileRepository _profiles;
    private readonly INeighborhoodInsightRepository _insights;
    private readonly IOptions<HousingOptions> _options;
    private readonly IClock _clock;

    public GetNeighborhoodDashboardQueryHandler(
        INeighborhoodProfileRepository profiles,
        INeighborhoodInsightRepository insights,
        IOptions<HousingOptions> options,
        IClock clock)
    {
        _profiles = profiles;
        _insights = insights;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodDashboardResponse>> Handle(
        GetNeighborhoodDashboardQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.UtcNow;
        var interval = _options.Value.NeighborhoodReviewIntervalDays;

        var page = await _profiles.QueryAsync(
            new NeighborhoodQueryFilter
            {
                CountryCode = request.CountryCode,
                OrderByPopularity = true
            },
            cursor: null,
            limit: SampleSize,
            cancellationToken);

        var profiles = page.Items;

        var pending = await _insights.QueryAsync(
            new NeighborhoodInsightQueryFilter
            {
                Statuses = [NeighborhoodInsightStatus.Submitted],
                IncludeUnapproved = true
            },
            cursor: null,
            limit: HousingLimits.MaxPageSize,
            cancellationToken);

        return Result.Success(new NeighborhoodDashboardResponse
        {
            TotalProfiles = profiles.Count,
            ApprovedProfiles = profiles.Count(profile => profile.IsApproved),

            NeedingReview = profiles.Count(
                profile => NeighborhoodMapper.NeedsReview(profile, now, interval)),

            // A place members are already living in, with nothing written about it, is the most
            // expensive gap in the directory: the demand is proven and the answer is missing.
            EmptyProfilesWithListings = profiles.Count(
                profile => profile.ListingCount > 0 && !profile.HasContent),

            PendingInsights = pending.Items.Count,

            MostActive =
            [
                .. profiles
                    .OrderByDescending(profile => profile.ListingCount)
                    .ThenByDescending(profile => profile.ApprovedInsightCount)
                    .Take(ListLength)
                    .Select(NeighborhoodMapper.ToSummaryResponse)
            ],

            LeastComplete =
            [
                .. profiles
                    .OrderBy(profile => profile.Completeness)
                    .ThenByDescending(profile => profile.ListingCount)
                    .Take(ListLength)
                    .Select(NeighborhoodMapper.ToSummaryResponse)
            ]
        });
    }
}
