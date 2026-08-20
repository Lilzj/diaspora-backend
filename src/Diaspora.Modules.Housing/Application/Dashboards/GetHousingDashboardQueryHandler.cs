using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Dashboards;

/// <summary>
/// Assembles the landlord's home screen.
/// <para>
/// <b>Counts rather than pages, everywhere it can.</b> A dashboard that loaded a page of each
/// collection to count it would be six queries returning data nobody renders, and it would report
/// "20+" for a landlord with a hundred properties.
/// </para>
/// <para>
/// <b>Uses <c>FindCallerLandlordAsync</c> rather than <c>RequireLandlordAsync</c>, deliberately.</b>
/// A suspended landlord must still be able to see their properties and read why they were
/// suspended; a home screen that went blank on suspension would leave them unable to correct
/// whatever caused it.
/// </para>
/// </summary>
public sealed class GetHousingDashboardQueryHandler
    : IQueryHandler<GetHousingDashboardQuery, HousingDashboardResponse>
{
    /// <summary>A week. Far enough ahead to plan, near enough that the list stays a list.</summary>
    private const int UpcomingDays = 7;

    private const int RecentListings = 5;

    private readonly IHousingListingRepository _listings;
    private readonly IHousingViewingRepository _viewings;
    private readonly IHousingReservationRepository _reservations;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingListingReadContext _read;
    private readonly IClock _clock;

    public GetHousingDashboardQueryHandler(
        IHousingListingRepository listings,
        IHousingViewingRepository viewings,
        IHousingReservationRepository reservations,
        IHousingAnalyticsRepository analytics,
        HousingAuthorizationService authorization,
        HousingListingReadContext read,
        IClock clock)
    {
        _listings = listings;
        _viewings = viewings;
        _reservations = reservations;
        _analytics = analytics;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<HousingDashboardResponse>> Handle(
        GetHousingDashboardQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var landlord = await _authorization.FindCallerLandlordAsync(cancellationToken);

        if (landlord is null)
        {
            return Result.Failure<HousingDashboardResponse>(HousingErrors.NotLandlord);
        }

        var now = _clock.UtcNow;

        var pending = await _viewings.CountAsync(
            new ViewingQueryFilter
            {
                LandlordId = landlord.Id,
                Statuses = [HousingViewingStatus.Requested]
            },
            cancellationToken);

        var awaitingDecision = await _reservations.CountAsync(
            new ReservationQueryFilter
            {
                LandlordId = landlord.Id,
                Statuses = [HousingReservationStatus.Requested]
            },
            cancellationToken);

        var upcoming = await _viewings.QueryAsync(
            new ViewingQueryFilter
            {
                LandlordId = landlord.Id,
                Statuses = [HousingViewingStatus.Scheduled],
                From = now,
                Until = now.AddDays(UpcomingDays)
            },
            cursor: null,
            limit: RecentListings,
            cancellationToken);

        var mine = new HousingListingQueryFilter
        {
            LandlordId = landlord.Id,
            ViewerId = _authorization.CallerId,
            ViewerLandlordId = landlord.Id,
            ViewerIsStaff = _authorization.IsStaff,
            IncludeNonPublic = true
        };

        var recent = await _listings.QueryAsync(
            mine with { Sort = HousingSortOrder.Newest },
            cursor: null,
            limit: RecentListings,
            cancellationToken);

        return Result.Success(new HousingDashboardResponse
        {
            Landlord = HousingLandlordMapper.ToSummary(landlord),

            // The private view: a landlord sees their own no-show count, and nobody else does.
            Statistics = HousingLandlordMapper.ToStatistics(landlord, includePrivate: true),

            PendingViewingRequests = pending,
            PendingReservations = awaitingDecision,

            UpcomingViewings =
            [
                .. upcoming.Items.Select(
                    viewing => HousingRelocationMapper.ToResponse(viewing, null, landlord))
            ],

            DraftListings = await CountAsync(mine, HousingListingStatus.Draft, cancellationToken),
            PublishedListings = await CountAsync(
                mine, HousingListingStatus.Available, cancellationToken),
            OccupiedListings = await CountAsync(
                mine, HousingListingStatus.Occupied, cancellationToken),

            // Actionable, so it belongs on the dashboard rather than in a report a landlord has to
            // go looking for. A flagged property is one that is not being seen.
            FlaggedListings = await _listings.CountAsync(
                mine with { ModerationState = HousingModerationState.Flagged }, cancellationToken),

            TotalViewsLast30Days = await _analytics.CountViewsForLandlordAsync(
                landlord.Id, now.AddDays(-30), cancellationToken),

            RecentListings = await _read.ToCardsAsync(recent.Items, cancellationToken)
        });
    }

    private async Task<int> CountAsync(
        HousingListingQueryFilter filter,
        HousingListingStatus status,
        CancellationToken cancellationToken) =>
        await _listings.CountAsync(filter with { Statuses = [status] }, cancellationToken);
}
