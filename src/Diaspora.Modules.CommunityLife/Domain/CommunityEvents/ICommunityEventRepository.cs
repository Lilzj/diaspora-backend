using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>How the module reads and writes events.</summary>
public interface ICommunityEventRepository
{
    Task<CommunityEvent?> GetAsync(
        CommunityEventId id, CancellationToken cancellationToken = default);

    /// <summary>An event by its deep-link handle. See the organization repository's note on links.</summary>
    /// <summary>
    /// An event including a soft-deleted one. The counterpart of the organization member of the
    /// same name, and for the same reason: an undelete that cannot load what it is undeleting fails
    /// silently, and so does a moderator trying to look at what was taken down.
    /// </summary>
    Task<CommunityEvent?> GetIncludingDeletedAsync(
        CommunityEventId id, CancellationToken cancellationToken = default);

    Task<CommunityEvent?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<CommunityEvent>> SearchAsync(
        EventQueryFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityEvent>> FindManyAsync(
        IReadOnlyCollection<CommunityEventId> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether the organizer already has an event with that title starting that day.</summary>
    Task<bool> HasDuplicateAsync(
        UserId organizerUserId,
        string? title,
        DateTimeOffset startsAt,
        CommunityEventId? excluding,
        CancellationToken cancellationToken = default);

    /// <summary>How many live events the organizer holds. The abuse bound.</summary>
    Task<int> CountLiveByOrganizerAsync(
        UserId organizerUserId, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>How many discoverable upcoming events sit in an area. B4's counter seam.</summary>
    Task<int> CountInAreaAsync(
        string areaPathPrefix, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(
        string countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>Published events whose start time has passed. The lifecycle sweep's work list.</summary>
    Task<IReadOnlyList<CommunityEvent>> FindStartedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Running events whose end time has passed. The completion sweep's work list.</summary>
    Task<IReadOnlyList<CommunityEvent>> FindEndedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Events in a window, for a calendar. Bounded rather than paged, because a calendar renders a
    /// month and a caller that wants more is asking a search question.
    /// </summary>
    Task<IReadOnlyList<CommunityEvent>> FindInWindowAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? countryCode,
        Guid? neighborhoodAreaId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Events with a coordinate inside a bounding box. What the nearby seam narrows with.</summary>
    Task<IReadOnlyList<CommunityEvent>> FindWithinBoxAsync(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        DateTimeOffset asOf,
        int limit,
        CancellationToken cancellationToken = default);

    // ── Occurrences ─────────────────────────────────────────────────────────────────────────
    //
    // B12.1. An occurrence is an event, so every read above serves one; what these add is the
    // series axis, which no other caller has any use for.

    /// <summary>
    /// One series' dates, soonest first, windowed and paged.
    /// <para>
    /// <b>Tie-broken on the sequence number rather than on the slug</b>, and that is the one place
    /// the occurrence page departs from every other page in the module. Within a series the sequence
    /// is unique, is an integer, and orders identically to the start time even when two occurrences
    /// have been moved onto the same instant — which the slug, allocated per date and unrelated to
    /// the pattern, does not.
    /// </para>
    /// </summary>
    Task<CursorPagedResult<CommunityEvent>> FindOccurrencesAsync(
        EventSeriesId seriesId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool includeNonPublic,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The occurrences a series edit has to rewrite: at or after a sequence, still ahead of the
    /// clock, and not individually edited.
    /// <para>
    /// The three conditions are the scope rules made executable. An occurrence that has happened
    /// cannot be moved, and one the organizer edited on its own is not the rule's to rewrite.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CommunityEvent>> FindOccurrencesToRewriteAsync(
        EventSeriesId seriesId,
        int fromSequence,
        DateTimeOffset notBefore,
        bool includeExceptions,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>The next date a series has that has not started. What a series card renders.</summary>
    Task<CommunityEvent?> FindNextOccurrenceAsync(
        EventSeriesId seriesId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The next date for each of several series, in two reads whatever the page size.
    /// <para>
    /// <b>The batched form exists because the single one is an N+1 the moment a page renders.</b>
    /// Twenty series asking for their own next date one at a time is twenty round trips to draw
    /// twenty cards — the same defect the event read context avoids for hosts and viewer responses,
    /// and it deserves the same answer.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<EventSeriesId, CommunityEvent>> FindNextOccurrencesAsync(
        IReadOnlyCollection<EventSeriesId> seriesIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The sequence numbers a series has already written.
    /// <para>
    /// What makes expansion idempotent: two sweeps racing, or one retried after a partial failure,
    /// must not write the same date twice. Read as a set of integers rather than as rows, because
    /// the sweep asks this before deciding whether to load anything.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<int>> FindOccurrenceSequencesAsync(
        EventSeriesId seriesId, CancellationToken cancellationToken = default);

    Task<int> CountOccurrencesAsync(
        EventSeriesId seriesId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Events these organisers first published in <c>[from, until)</c>, and how many of them
    /// organised one — 21 Oct 2026, for a community's statistics. A cancelled event was still
    /// published, so it counts; a deleted one does not.
    /// </summary>
    Task<(int EventCount, int OrganizerCount)> CountFirstPublishedByOrganizersAsync(
        IReadOnlyCollection<Guid> organizerUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);

    Task AddAsync(CommunityEvent communityEvent, CancellationToken cancellationToken = default);

    void Update(CommunityEvent communityEvent);
}
