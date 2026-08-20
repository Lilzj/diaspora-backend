using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Calendar;

/// <summary>
/// The mapping between a Diaspora entity and the calendar entry written for it.
/// <para>
/// <b>This is the duplicate-suppression design, and it is a unique index rather than a check.</b>
/// The register is unambiguous about what happens without it: "a retry after a timeout is a
/// DUPLICATE EVENT in somebody's calendar — the most visible possible failure of this integration,
/// and one a member cannot easily undo." A handler-level "have I written this already" loses a race
/// between two workers, and unattended execution (Phase 18 GAP-025) makes concurrent attempts
/// likelier rather than less.
/// </para>
/// <para>
/// <b>The uniqueness is on (connection, calendar, source), not on (source).</b> Exporting one event
/// to a work calendar and a personal calendar is two legitimate entries; exporting it to one
/// calendar twice is a defect. A key without the calendar would forbid the first, and a key without
/// the connection would let one member's export suppress another's.
/// </para>
/// <para>
/// <b><see cref="IdempotencyKey"/> is separate from that key and answers a different question.</b>
/// The natural key answers "does an entry exist"; the idempotency key answers "is this the same
/// attempt". A member legitimately updating an exported event sends a new key against the same
/// natural key, and a retried timeout sends the same key — collapsing the two into one column would
/// make an update indistinguishable from a retry.
/// </para>
/// </summary>
public sealed class CalendarEventLink : AggregateRoot<CalendarEventLinkId>
{
    public const int SourceIdLength = 128;
    public const int SourceKindLength = 32;
    public const int CalendarIdLength = 256;
    public const int ExternalEventIdLength = 256;
    public const int ViewUrlLength = 1024;

    /// <summary>A SHA-256 in hex. Client keys are hashed rather than stored; see the property.</summary>
    public const int IdempotencyKeyLength = 64;

    /// <summary>An IANA zone identifier. The longest in the database is well under this.</summary>
    public const int TimeZoneLength = 64;

    /// <summary>A content fingerprint. Hex SHA-256.</summary>
    public const int ContentHashLength = 64;

    private CalendarEventLink(
        CalendarEventLinkId id,
        IntegrationConnectionId connectionId,
        Guid ownerId,
        string calendarId,
        string sourceKind,
        string sourceId,
        string externalEventId,
        string timeZone,
        string contentHash,
        DateTimeOffset writtenAt) : base(id)
    {
        ConnectionId = connectionId;
        OwnerId = ownerId;
        CalendarId = calendarId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        ExternalEventId = externalEventId;
        TimeZone = timeZone;
        ContentHash = contentHash;
        State = CalendarLinkState.Written;
        WrittenAt = writtenAt;
        LastAttemptedAt = writtenAt;
    }

    private CalendarEventLink() { }

    public IntegrationConnectionId ConnectionId { get; private set; } = default!;

    /// <summary>
    /// Denormalised from the connection.
    /// <para>
    /// So that "delete everything this member's integration produced" is one indexed query rather
    /// than a join, which matters when the member is exercising an erasure right and the connection
    /// they are erasing is the row that would be joined through.
    /// </para>
    /// </summary>
    public Guid OwnerId { get; private set; }

    public string CalendarId { get; private set; } = string.Empty;

    /// <summary><c>event</c>, <c>occurrence</c>, <c>series</c> or <c>reminder</c>.</summary>
    public string SourceKind { get; private set; } = string.Empty;

    /// <summary>Diaspora's own identifier for the thing exported.</summary>
    public string SourceId { get; private set; } = string.Empty;

    public string ExternalEventId { get; private set; } = string.Empty;

    public string? ViewUrl { get; private set; }

    /// <summary>
    /// The zone the entry was written in.
    /// <para>
    /// Recorded because it is a claim about a real-world moment: "a community meeting in Lagos is at
    /// 18:00 WAT whether or not the member is currently in London". Storing it means a later
    /// correction can tell whether the entry is wrong or merely elsewhere.
    /// </para>
    /// </summary>
    public string TimeZone { get; private set; } = string.Empty;

    /// <summary>
    /// A fingerprint of what was written.
    /// <para>
    /// What makes a repeated export a no-op rather than a pointless provider call: an unchanged
    /// event re-exported by a nightly sync hashes to the same value and is skipped. Not a security
    /// control — the values behind it are the member's own — and so a plain hash of the fields.
    /// </para>
    /// </summary>
    public string ContentHash { get; private set; } = string.Empty;

    public CalendarLinkState State { get; private set; }

    /// <summary>
    /// The hashed <c>Idempotency-Key</c> of the attempt that produced the current state.
    /// <para>
    /// Hashed rather than stored, because a client is free to put anything in that header and this
    /// platform should not become the place where it is retained in the clear.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset WrittenAt { get; private set; }

    public DateTimeOffset LastAttemptedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static CalendarEventLink Record(
        CalendarEventLinkId id,
        IntegrationConnectionId connectionId,
        Guid ownerId,
        string calendarId,
        string sourceKind,
        string sourceId,
        string externalEventId,
        string timeZone,
        string contentHash,
        string? idempotencyKey,
        string? viewUrl,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);

        return new CalendarEventLink(
            id,
            connectionId,
            ownerId,
            Clip(calendarId, CalendarIdLength),
            Clip(sourceKind, SourceKindLength),
            Clip(sourceId, SourceIdLength),
            Clip(externalEventId, ExternalEventIdLength),
            Clip(timeZone, TimeZoneLength),
            Clip(contentHash, ContentHashLength),
            now)
        {
            IdempotencyKey = HashKey(idempotencyKey),
            ViewUrl = string.IsNullOrWhiteSpace(viewUrl) ? null : Clip(viewUrl, ViewUrlLength)
        };
    }

    /// <summary>The provider accepted a replacement for the same source.</summary>
    public void RecordUpdate(
        string externalEventId,
        string timeZone,
        string contentHash,
        string? idempotencyKey,
        string? viewUrl,
        DateTimeOffset now)
    {
        ExternalEventId = Clip(externalEventId, ExternalEventIdLength);
        TimeZone = Clip(timeZone, TimeZoneLength);
        ContentHash = Clip(contentHash, ContentHashLength);
        IdempotencyKey = HashKey(idempotencyKey);
        ViewUrl = string.IsNullOrWhiteSpace(viewUrl) ? ViewUrl : Clip(viewUrl, ViewUrlLength);
        State = CalendarLinkState.Written;
        RemovedAt = null;
        WrittenAt = now;
        LastAttemptedAt = now;
    }

    /// <summary>Diaspora removed the entry. Idempotent: removing twice does not move the moment.</summary>
    public void RecordRemoval(DateTimeOffset now)
    {
        if (State is CalendarLinkState.Removed)
        {
            return;
        }

        State = CalendarLinkState.Removed;
        RemovedAt = now;
        LastAttemptedAt = now;
    }

    /// <summary>
    /// The entry is gone and Diaspora did not remove it.
    /// <para>
    /// A member deleting an exported entry in their own calendar is a legitimate act, and the
    /// distinction from <see cref="RecordRemoval"/> is what stops a sync putting it back.
    /// </para>
    /// </summary>
    public void RecordRemovalAtProvider(DateTimeOffset now)
    {
        State = CalendarLinkState.RemovedAtProvider;
        RemovedAt = now;
        LastAttemptedAt = now;
    }

    public void RecordAttempt(DateTimeOffset now) => LastAttemptedAt = now;

    /// <summary>Whether a write with this fingerprint would change anything.</summary>
    public bool Matches(string contentHash) =>
        State is CalendarLinkState.Written
        && string.Equals(ContentHash, contentHash, StringComparison.Ordinal);

    /// <summary>Whether this row already records the outcome of <paramref name="idempotencyKey"/>.</summary>
    public bool WasWrittenBy(string? idempotencyKey) =>
        HashKey(idempotencyKey) is { } hashed
        && string.Equals(IdempotencyKey, hashed, StringComparison.Ordinal);

    /// <summary>
    /// A stable fingerprint of everything a provider would be told.
    /// <para>
    /// Every field that reaches the provider and nothing that does not. Attendees are absent because
    /// they are never exported — "exporting an event must not disclose who else is going to a third
    /// party the other attendees never agreed to" — so including them would make the hash change for
    /// a reason the provider can never see.
    /// </para>
    /// </summary>
    public static string Fingerprint(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string timeZone,
        bool isAllDay,
        string? location,
        string? notes,
        string? url,
        IReadOnlyList<int> reminderMinutes,
        string? recurrenceRule)
    {
        // ASCII unit separator between fields. A character no calendar title contains, so
        // "Standup" + "Friday" and "Standup Friday" + "" cannot hash alike — which a naive
        // concatenation would allow, and which would silently suppress a legitimate update.
        const char Separator = '\u001f';

        var builder = new StringBuilder()
            .Append(title).Append(Separator)
            .Append(startsAt.ToUniversalTime().ToString("O")).Append(Separator)
            .Append(endsAt.ToUniversalTime().ToString("O")).Append(Separator)
            .Append(timeZone).Append(Separator)
            .Append(isAllDay).Append(Separator)
            .Append(location).Append(Separator)
            .Append(notes).Append(Separator)
            .Append(url).Append(Separator)
            .Append(string.Join(',', (reminderMinutes ?? []).Order())).Append(Separator)
            .Append(recurrenceRule);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string? HashKey(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));

    private static string Clip(string value, int length)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}
