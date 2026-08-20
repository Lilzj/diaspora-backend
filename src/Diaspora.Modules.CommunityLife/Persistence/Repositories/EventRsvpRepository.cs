using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for responses.
/// <para>
/// <b><see cref="SumPlacesAsync"/> sums party sizes rather than counting rows, and that is the whole
/// difference between a correct attendance figure and a wrong one.</b> "Me and three others" is how
/// a family responds to a naming ceremony; a count of rows would tell an organizer to lay out a
/// quarter of the chairs they need.
/// </para>
/// <para>
/// <b><see cref="FindWaitlistAsync"/> orders by creation and that ordering is the feature.</b>
/// Promoting whoever the database returned first would make a waiting list a lottery.
/// </para>
/// </summary>
public sealed class EventRsvpRepository : IEventRsvpRepository
{
    private static readonly RsvpStatus[] LiveStatuses =
    [
        RsvpStatus.Interested,
        RsvpStatus.Going,
        RsvpStatus.Waitlisted,
        RsvpStatus.CheckedIn
    ];

    private readonly DiasporaDbContext _dbContext;

    public EventRsvpRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<EventRsvp> Rsvps => _dbContext.Set<EventRsvp>();

    private IQueryable<CommunityEvent> Events => _dbContext.Set<CommunityEvent>().AsNoTracking();

    public async Task<EventRsvp?> GetAsync(
        EventRsvpId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Rsvps
            .Include(rsvp => rsvp.History)
            .FirstOrDefaultAsync(rsvp => rsvp.Id == id, cancellationToken);
    }

    public async Task<EventRsvp?> FindAsync(
        CommunityEventId eventId,
        UserId attendeeUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(attendeeUserId);

        return await Rsvps
            .Include(rsvp => rsvp.History)
            .FirstOrDefaultAsync(
                rsvp => rsvp.EventId == eventId && rsvp.AttendeeUserId == attendeeUserId,
                cancellationToken);
    }

    public async Task<CursorPagedResult<EventRsvp>> SearchAsync(
        RsvpQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Rsvps.AsNoTracking();

        if (filter.EventId is { } eventId)
        {
            query = query.Where(rsvp => rsvp.EventId == eventId);
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(rsvp => rsvp.OrganizationId == organizationId);
        }

        if (filter.AttendeeUserId is { } attendeeUserId)
        {
            query = query.Where(rsvp => rsvp.AttendeeUserId == attendeeUserId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(rsvp => statuses.Contains(rsvp.Status));
        }

        // Correlated against the event rather than the response, because an RSVP row has no start
        // time of its own. Translated to an EXISTS over an indexed identifier, which is why this is
        // written as a subquery rather than a join returning a pair the caller would discard.
        if (filter.UpcomingAsOf is { } asOf)
        {
            query = query.Where(rsvp => Events.Any(
                communityEvent => communityEvent.Id == rsvp.EventId
                    && communityEvent.Schedule.EndsAt >= asOf));
        }

        var parsed = CommunityLifeCursor.Parse(filter.Cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastKey, EventRsvpId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(rsvp =>
                rsvp.CreatedAt < boundary
                || (rsvp.CreatedAt == boundary && !exhausted && !seen.Contains(rsvp.Id)));
        }

        query = query
            .OrderByDescending(rsvp => rsvp.CreatedAt)
            .ThenBy(rsvp => rsvp.Id);

        var limit = Math.Clamp(filter.Limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityLifeCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<EventRsvp>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<EventRsvp>> FindForAttendeeAsync(
        UserId attendeeUserId,
        IReadOnlyCollection<CommunityEventId> eventIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attendeeUserId);
        ArgumentNullException.ThrowIfNull(eventIds);

        if (eventIds.Count == 0)
        {
            return [];
        }

        var ids = eventIds.Distinct().ToList();

        return await Rsvps
            .AsNoTracking()
            .Where(rsvp => rsvp.AttendeeUserId == attendeeUserId && ids.Contains(rsvp.EventId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<RsvpStatus, int>> CountByStatusAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        var rows = await Rsvps
            .AsNoTracking()
            .Where(rsvp => rsvp.EventId == eventId)
            .GroupBy(rsvp => rsvp.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count);
    }

    public async Task<RsvpPlaceTotals> SumPlacesAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        // One grouped round trip rather than four aggregates. Summed over party size — see the
        // class summary for why counting rows is the wrong answer.
        var rows = await Rsvps
            .AsNoTracking()
            .Where(rsvp => rsvp.EventId == eventId)
            .GroupBy(rsvp => rsvp.Status)
            .Select(group => new StatusPlaces(group.Key, group.Sum(rsvp => rsvp.PartySize)))
            .ToListAsync(cancellationToken);

        // Bucketed through the same predicates the settlement path uses, so a recount and a delta
        // cannot disagree about which statuses hold a place. Going therefore includes everybody who
        // has been through the door: a check-in does not release the place it occupies.
        return new RsvpPlaceTotals(
            Sum(rows, RsvpStatusTransitions.HoldsCapacity),
            Sum(rows, static status => status == RsvpStatus.Interested),
            Sum(rows, static status => status == RsvpStatus.Waitlisted),
            Sum(rows, RsvpStatusTransitions.IsPresent));

        static int Sum(IReadOnlyList<StatusPlaces> source, Func<RsvpStatus, bool> isInBucket) =>
            source.Where(row => isInBucket(row.Status)).Sum(row => row.Places);
    }

    /// <summary>One grouped row of the place recount. Named so the buckets can be a shared lambda.</summary>
    private sealed record StatusPlaces(RsvpStatus Status, int Places);

    public async Task<IReadOnlyList<EventRsvp>> FindWaitlistAsync(
        CommunityEventId eventId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        return await Rsvps
            .Where(rsvp => rsvp.EventId == eventId && rsvp.Status == RsvpStatus.Waitlisted)
            .OrderBy(rsvp => rsvp.CreatedAt)
            .Take(Math.Clamp(limit, 1, CommunityLifeLimits.MaxPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// How many waitlisted responses joined before a given one.
    /// <para>
    /// Strictly before, on the same creation timestamp <see cref="FindWaitlistAsync"/> promotes by.
    /// Two responses created inside one clock tick tie, and both are told the same position — which
    /// is honest, because promotion between them is genuinely undefined and telling one of them they
    /// are second when they might be promoted first would be worse.
    /// </para>
    /// </summary>
    public async Task<int> CountWaitlistedAheadAsync(
        CommunityEventId eventId,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        return await Rsvps
            .AsNoTracking()
            .CountAsync(
                rsvp => rsvp.EventId == eventId
                    && rsvp.Status == RsvpStatus.Waitlisted
                    && rsvp.CreatedAt < joinedAt,
                cancellationToken);
    }

    public async Task<IReadOnlyList<EventRsvp>> FindLiveAsync(
        CommunityEventId eventId, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        return await Rsvps
            .Where(rsvp => rsvp.EventId == eventId && LiveStatuses.Contains(rsvp.Status))
            .OrderBy(rsvp => rsvp.CreatedAt)
            .Take(Math.Clamp(batchSize, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The addressable set: still holding a place, or released because the event was called off.
    /// <para>
    /// Untracked, because every caller is a read on a fan-out path. See the interface for why the
    /// second half of the predicate is not an accident.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<EventRsvp>> FindAddressableAsync(
        CommunityEventId eventId, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        return await Rsvps
            .AsNoTracking()
            .Where(rsvp => rsvp.EventId == eventId)
            .Where(rsvp => LiveStatuses.Contains(rsvp.Status)
                || rsvp.DeclineReason == RsvpDeclineReason.EventCancelled)
            .OrderBy(rsvp => rsvp.CreatedAt)
            .Take(Math.Clamp(batchSize, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        CommunityEventId eventId,
        UserId attendeeUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(attendeeUserId);

        return await Rsvps
            .AsNoTracking()
            .AnyAsync(
                rsvp => rsvp.EventId == eventId && rsvp.AttendeeUserId == attendeeUserId,
                cancellationToken);
    }

    public async Task AddAsync(EventRsvp rsvp, CancellationToken cancellationToken = default) =>
        await Rsvps.AddAsync(rsvp, cancellationToken);

    public void Update(EventRsvp rsvp) => Rsvps.Update(rsvp);
}
