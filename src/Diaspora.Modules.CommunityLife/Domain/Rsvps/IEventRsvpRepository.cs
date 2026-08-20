using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>
/// How the module reads and writes responses.
/// <para>
/// There is deliberately no method that returns every response to an event as a list. The attendee
/// list is paged everywhere it is rendered, and a repository that offered an unbounded read would
/// be the one a well-meaning handler used to render a card.
/// </para>
/// </summary>
public interface IEventRsvpRepository
{
    Task<EventRsvp?> GetAsync(EventRsvpId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The response joining one member to one event, whatever its status.
    /// <para>
    /// Includes withdrawn responses, and that is load-bearing: a member changing their mind twice
    /// must move the response they already have rather than creating a second one, or the counters
    /// would be settled twice for one person.
    /// </para>
    /// </summary>
    Task<EventRsvp?> FindAsync(
        CommunityEventId eventId,
        UserId attendeeUserId,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<EventRsvp>> SearchAsync(
        RsvpQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// One member's responses to a named set of events, in one read.
    /// <para>
    /// What a page of cards needs to show the viewer what they already said. Asking per card would
    /// be twenty queries to render twenty badges, and the pageable search cannot answer it because
    /// its cursor would cut the set short.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> FindForAttendeeAsync(
        UserId attendeeUserId,
        IReadOnlyCollection<CommunityEventId> eventIds,
        CancellationToken cancellationToken = default);

    /// <summary>How many responses an event has in each status, and how many places they hold.</summary>
    Task<IReadOnlyDictionary<RsvpStatus, int>> CountByStatusAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The true place counts for an event, summing party sizes rather than rows.
    /// <para>
    /// The reconciliation read. A count of rows would be wrong the moment anybody brought their
    /// family, which is most of them.
    /// </para>
    /// </summary>
    Task<RsvpPlaceTotals> SumPlacesAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The waiting list, in the order people joined it.
    /// <para>
    /// Order matters and is the whole point: promoting whoever the database returned first would
    /// make a waiting list a lottery.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> FindWaitlistAsync(
        CommunityEventId eventId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many waitlisted responses joined the queue before a given one.
    /// <para>
    /// B12.1. "You are fourth" is a fact about every other response to that event, so the server has
    /// to answer it: a client could only derive it from the whole guest list, which is exactly what
    /// an attendee is not allowed to read.
    /// </para>
    /// <para>
    /// <b>Counted on the same column <see cref="FindWaitlistAsync"/> orders by</b> — the creation
    /// time, not the response time. The two differ the moment somebody moves from interested to
    /// waitlisted, and a position counted on one while promotion ran on the other would tell a
    /// member they were fourth and then promote the seventh.
    /// </para>
    /// </summary>
    Task<int> CountWaitlistedAheadAsync(
        CommunityEventId eventId,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Every live response to an event. The cancellation sweep's work list, batched.</summary>
    Task<IReadOnlyList<EventRsvp>> FindLiveAsync(
        CommunityEventId eventId, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everybody who should hear about a change to the event — B13.3.
    /// <para>
    /// <b>Live responses, plus the ones the platform released when the event was called off.</b>
    /// The second half is the whole reason this exists and not <see cref="FindLiveAsync"/>:
    /// cancelling an event releases every place inside the same transaction, so by the time anything
    /// downstream asks who was affected, nobody is live and the honest answer to "who should be told
    /// this was cancelled" is a list that no longer exists. The decline reason is what separates
    /// those members from the ones who withdrew of their own accord — and telling somebody who
    /// pulled out last month that the event is off is a message they did not need.
    /// </para>
    /// <para>Bounded like every other fan-out query. The caller pages or accepts the cap.</para>
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> FindAddressableAsync(
        CommunityEventId eventId, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Whether the member has ever responded to the event.</summary>
    Task<bool> ExistsAsync(
        CommunityEventId eventId,
        UserId attendeeUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(EventRsvp rsvp, CancellationToken cancellationToken = default);

    void Update(EventRsvp rsvp);
}
