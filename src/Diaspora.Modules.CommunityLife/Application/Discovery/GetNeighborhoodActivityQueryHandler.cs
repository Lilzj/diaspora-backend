using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// How much is happening in one neighbourhood.
/// <para>
/// <b>B12.1, and the plumbing was already here.</b> The analytics table has carried per-neighbourhood
/// totals since B12 and <c>NeighborhoodActivityResponse</c> has existed unused since then; what was
/// missing was a route to it. Nothing new is stored — this is a read over a table that was already
/// being written.
/// </para>
/// <para>
/// <b>The place name comes from B4 through the resolver, never from a copy kept here.</b> An area
/// this module had cached a name for would be the one that goes stale when Location renames it.
/// </para>
/// <para>
/// <b>An unknown area is a not-found; a known one with nothing in it answers zeroes.</b> The two are
/// genuinely different questions and collapsing them would tell somebody that a quiet neighbourhood
/// does not exist.
/// </para>
/// </summary>
public sealed class GetNeighborhoodActivityQueryHandler
    : IQueryHandler<GetNeighborhoodActivityQuery, NeighborhoodActivityResponse>
{
    private const int DefaultWindowDays = 365;

    private readonly IEventAnalyticsRepository _analytics;
    private readonly IOrganizationRepository _organizations;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly IClock _clock;

    public GetNeighborhoodActivityQueryHandler(
        IEventAnalyticsRepository analytics,
        IOrganizationRepository organizations,
        CommunityLifeNeighborhoodResolver neighborhoods,
        IClock clock)
    {
        _analytics = analytics;
        _organizations = organizations;
        _neighborhoods = neighborhoods;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodActivityResponse>> Handle(
        GetNeighborhoodActivityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var areaPath = await _neighborhoods.AreaPathAsync(query.AreaId, cancellationToken);

        if (areaPath is null)
        {
            return Result.Failure<NeighborhoodActivityResponse>(
                CommunityLifeErrors.NeighborhoodNotFound);
        }

        var now = _clock.UtcNow;
        var from = now.AddDays(-Math.Clamp(query.WindowDays ?? DefaultWindowDays, 1, 1_825));

        var activity = await _analytics.SummariseNeighborhoodAsync(
            query.AreaId, from, now, cancellationToken);

        // An organization is not bounded in time the way an event is, so its count takes no window:
        // a church that has been in Peckham for thirty years is still there this year.
        var organizations = await _organizations.CountInAreaAsync(areaPath, cancellationToken);

        var profile = await _neighborhoods.ProfileAsync(query.AreaId, cancellationToken);

        return Result.Success(new NeighborhoodActivityResponse
        {
            AreaId = query.AreaId,
            Name = profile?.Name,
            EventCount = activity?.EventCount ?? 0,
            OrganizationCount = organizations,
            TotalRsvps = activity?.TotalRsvps ?? 0,
            TotalAttendances = activity?.TotalAttendances ?? 0
        });
    }
}
