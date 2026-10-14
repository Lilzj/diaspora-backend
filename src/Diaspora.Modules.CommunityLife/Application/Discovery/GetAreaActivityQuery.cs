using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Geography;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// How much community life there is in the area around a point (14 Oct 2026, Phase 2A of retiring
/// the place catalogue). The point-keyed successor of <see cref="GetNeighborhoodActivityQuery"/>.
/// <para>
/// Anonymous, like every other discovery read.
/// </para>
/// </summary>
public sealed record GetAreaActivityQuery : IQuery<AreaActivityResponse>
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>Kilometres. 2 when null; refused outside (0, 10].</summary>
    public double? RadiusKm { get; init; }

    /// <summary>The Google place name the member picked, echoed back. Never inferred.</summary>
    public string? Name { get; init; }

    /// <summary>How far back to count, in days. Defaults to a year.</summary>
    public int? WindowDays { get; init; }
}

/// <summary>
/// Counts what is happening within a circle, from each event's venue and each organization's own
/// pin.
/// <para>
/// <b>Rows with no coordinates are not within anything.</b> An online event, or an organization that
/// never gave a pin, is not counted; the response says so rather than guessing where it is.
/// </para>
/// </summary>
public sealed class GetAreaActivityQueryHandler : IQueryHandler<GetAreaActivityQuery, AreaActivityResponse>
{
    public const double DefaultRadiusKm = 2d;

    public const double MaximumRadiusKm = 10d;

    private const int DefaultWindowDays = 365;

    /// <summary>The most rows read for one circle, per source. Reaching it marks the counts as at least.</summary>
    private const int MaximumRowsRead = 1_000;

    /// <summary>The event repository's own cap on one box read.</summary>
    private const int UpcomingBoxCap = 200;

    private readonly IEventAnalyticsRepository _analytics;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IClock _clock;

    public GetAreaActivityQueryHandler(
        IEventAnalyticsRepository analytics,
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IClock clock)
    {
        _analytics = analytics;
        _events = events;
        _organizations = organizations;
        _clock = clock;
    }

    public async Task<Result<AreaActivityResponse>> Handle(
        GetAreaActivityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var radius = query.RadiusKm ?? DefaultRadiusKm;

        if (double.IsNaN(query.Latitude) || query.Latitude is < -90d or > 90d
            || double.IsNaN(query.Longitude) || query.Longitude is < -180d or > 180d
            || double.IsNaN(radius) || radius <= 0d || radius > MaximumRadiusKm)
        {
            return Result.Failure<AreaActivityResponse>(CommunityLifeErrors.AreaPointInvalid);
        }

        var circle = CircleArea.Around(query.Latitude, query.Longitude, radius);
        var now = _clock.UtcNow;
        var windowDays = Math.Clamp(query.WindowDays ?? DefaultWindowDays, 1, 1_825);
        var from = now.AddDays(-windowDays);

        var activity = await _analytics.FindActivityWithinBoxAsync(
            circle.South, circle.North, circle.West, circle.East, from, now, MaximumRowsRead, cancellationToken);

        var inside = activity.Where(row => circle.Contains(row.Latitude, row.Longitude)).ToList();

        var upcoming = await _events.FindWithinBoxAsync(
            circle.South, circle.North, circle.West, circle.East, now, UpcomingBoxCap, cancellationToken);

        var (organizations, organizationsRead) = await CountOrganizationsAsync(circle, cancellationToken);

        var name = string.IsNullOrWhiteSpace(query.Name) ? null : query.Name.Trim();

        if (name is { Length: > 200 })
        {
            name = name[..200];
        }

        return Result.Success(new AreaActivityResponse
        {
            Latitude = query.Latitude,
            Longitude = query.Longitude,
            RadiusKm = radius,
            Name = name,
            DisplayName = name ?? string.Format(
                System.Globalization.CultureInfo.InvariantCulture, "Within {0:0.##} km of you", radius),
            WindowDays = windowDays,
            EventCount = inside.Count,
            UpcomingEventCount = upcoming.Count(communityEvent =>
                circle.Contains(communityEvent.Venue.Latitude, communityEvent.Venue.Longitude)),
            OrganizationCount = organizations,
            TotalRsvps = inside.Sum(row => row.RsvpCount),
            TotalAttendances = inside.Sum(row => row.AttendedCount),
            IsLowerBound = activity.Count >= MaximumRowsRead
                || upcoming.Count >= UpcomingBoxCap
                || organizationsRead >= MaximumRowsRead
        });
    }

    /// <summary>
    /// Public organizations pinned inside the circle, read through the search's own query so its
    /// audience rule (public only, no viewer) holds by construction.
    /// </summary>
    private async Task<(int Inside, int Read)> CountOrganizationsAsync(
        CircleArea circle, CancellationToken cancellationToken)
    {
        var filter = new OrganizationQueryFilter
        {
            MinLatitude = circle.South,
            MaxLatitude = circle.North,
            MinLongitude = circle.West,
            MaxLongitude = circle.East,
            IncludeNonPublic = false,
            Limit = CommunityLifeLimits.MaxPageSize
        };

        var read = 0;
        var inside = 0;

        while (read < MaximumRowsRead)
        {
            var page = await _organizations.SearchAsync(filter, cancellationToken);

            read += page.Items.Count;
            inside += page.Items.Count(organization => circle.Contains(organization.Latitude, organization.Longitude));

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            filter = filter with { Cursor = page.NextCursor };
        }

        return (inside, read);
    }
}
