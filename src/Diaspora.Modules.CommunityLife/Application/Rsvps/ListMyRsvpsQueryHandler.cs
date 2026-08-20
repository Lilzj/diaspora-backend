using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Rsvps;

/// <summary>
/// What the caller has said they are going to.
/// <para>
/// <b>The events travel beside the responses rather than inside them.</b> A response says a status
/// and a party size; what somebody actually wants to see is where they are going and when. Loading
/// the events in one batched read keeps the page to two queries however long it is.
/// </para>
/// <para>
/// "Upcoming" is answered against the event's end time, so something running now is still upcoming —
/// a member checking what they are meant to be at this evening should find it there.
/// </para>
/// </summary>
public sealed class ListMyRsvpsQueryHandler : IQueryHandler<ListMyRsvpsQuery, RsvpPageResponse>
{
    private readonly IEventRsvpRepository _rsvps;
    private readonly ICommunityEventRepository _events;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IClock _clock;

    public ListMyRsvpsQueryHandler(
        IEventRsvpRepository rsvps,
        ICommunityEventRepository events,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        IClock clock)
    {
        _rsvps = rsvps;
        _events = events;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<RsvpPageResponse>> Handle(
        ListMyRsvpsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<RsvpPageResponse>(caller.Error);
        }

        var page = await _rsvps.SearchAsync(
            new RsvpQueryFilter
            {
                AttendeeUserId = caller.Value,
                Statuses = ParseStatuses(query.Statuses),
                UpcomingAsOf = query.UpcomingOnly ? _clock.UtcNow : null,
                Cursor = query.Cursor,
                Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        var events = page.Items.Count == 0
            ? []
            : await _events.FindManyAsync(
                [.. page.Items.Select(rsvp => rsvp.EventId).Distinct()], cancellationToken);

        // Written as a lambda rather than a method group on purpose. CommunityEventMapper.ToResponse
        // grew optional parameters in B12.1, and a method group with an optional int binds to
        // Select's indexed overload instead — which compiles, passes the row's position as the
        // waitlist position, and is wrong on every row but the first. The platform has paid for
        // that one before.
        var series = events.ToDictionary(
            communityEvent => communityEvent.Id,
            communityEvent => communityEvent.SeriesId?.Value);

        var positions = await WaitlistPositionsAsync(page.Items, cancellationToken);

        return Result.Success(new RsvpPageResponse
        {
            Items =
            [
                .. page.Items.Select(rsvp => CommunityEventMapper.ToResponse(
                    rsvp,
                    positions.GetValueOrDefault(rsvp.Id),
                    series.GetValueOrDefault(rsvp.EventId)))
            ],
            Events = await _read.ToSummariesAsync(
                events, cancellationToken: cancellationToken),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    /// <summary>
    /// Queue positions for whichever rows on this page are waitlisted.
    /// <para>
    /// One query per waitlisted row, and bounded by that rather than by the page: a member typically
    /// has none and rarely more than one or two. Counting them all in a single grouped query would
    /// need a correlated subquery per row anyway, and would run it for the ninety-eight rows that
    /// are not waiting for anything.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<EventRsvpId, int>> WaitlistPositionsAsync(
        IReadOnlyList<EventRsvp> rsvps, CancellationToken cancellationToken)
    {
        var positions = new Dictionary<EventRsvpId, int>();

        foreach (var rsvp in rsvps.Where(rsvp => rsvp.Status == RsvpStatus.Waitlisted))
        {
            var ahead = await _rsvps.CountWaitlistedAheadAsync(
                rsvp.EventId, rsvp.CreatedAt, cancellationToken);

            positions[rsvp.Id] = ahead + 1;
        }

        return positions;
    }

    private static List<RsvpStatus>? ParseStatuses(IReadOnlyList<string>? statuses)
    {
        if (statuses is not { Count: > 0 })
        {
            return null;
        }

        var parsed = statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<RsvpStatus>)
            .OfType<RsvpStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }
}
