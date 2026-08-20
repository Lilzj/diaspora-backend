using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Sync;

/// <summary>
/// One synchronisation, from accepted to terminal.
/// <para>
/// <b>A sync is accepted, not completed, and this row is why the difference is expressible.</b> The
/// register's contract returns <c>{ syncId, phase: "syncing", startedAt }</c> — "accepted, not
/// completed" — and the client's <c>withSync()</c> has "no path from a local success to
/// <c>SYNCED</c>". Both sides depend on the server owning a phase that a client can read back, and
/// this is it.
/// </para>
/// <para>
/// <b>Only one run per (owner, provider, calendar) may be live.</b> "Two concurrent syncs over one
/// calendar is how duplicates are produced." The barrier is two things and needs both: a partial
/// unique index would be the natural fit and is not portable across this platform's providers, so
/// it is a repository lookup for the running run — returning it rather than starting a second, as
/// the contract requires — under a distributed lease taken for the same key. The lookup makes the
/// common case correct and cheap; the lease is what makes two instances racing correct as well.
/// </para>
/// <para>
/// <b><see cref="ItemsFailed"/> and <see cref="FailureReasons"/> are what make
/// <see cref="SyncPhase.Partial"/> honest.</b> A run that wrote eleven of twelve reports eleven and
/// one, and the reasons deduplicated — not a single boolean that would round the answer to a lie in
/// either direction.
/// </para>
/// </summary>
public sealed class IntegrationSyncRun : AggregateRoot<IntegrationSyncRunId>
{
    public const int CalendarIdLength = 256;

    /// <summary>The joined, deduplicated reasons. Member-facing sentences, never provider payloads.</summary>
    public const int FailureReasonsLength = 2000;

    private const char ReasonSeparator = '\n';

    /// <summary>
    /// How long a run may stay <see cref="SyncPhase.Syncing"/> before the sweep declares it lost.
    /// <para>
    /// A run whose process died mid-way leaves a row claiming to be in progress, and that row is
    /// also the concurrency barrier — so without a reaper, one crash means that calendar can never
    /// be synchronised again. Thirty minutes is far above any real run and far below "nobody
    /// noticed".
    /// </para>
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private IntegrationSyncRun(
        IntegrationSyncRunId id,
        IntegrationConnectionId connectionId,
        Guid ownerId,
        string provider,
        string? calendarId,
        SyncDirection direction,
        DateTimeOffset startedAt) : base(id)
    {
        ConnectionId = connectionId;
        OwnerId = ownerId;
        Provider = provider;
        CalendarId = calendarId;
        Direction = direction;
        Phase = SyncPhase.Syncing;
        StartedAt = startedAt;
    }

    private IntegrationSyncRun() { }

    public IntegrationConnectionId ConnectionId { get; private set; } = default!;

    public Guid OwnerId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    /// <summary>Null for a provider-wide run. Part of the concurrency key when present.</summary>
    public string? CalendarId { get; private set; }

    public SyncDirection Direction { get; private set; }

    public SyncPhase Phase { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int ItemsSynced { get; private set; }

    public int ItemsFailed { get; private set; }

    private string? _failureReasons;

    /// <summary>Why the failed ones failed, deduplicated.</summary>
    public IReadOnlyList<string> FailureReasons =>
        string.IsNullOrWhiteSpace(_failureReasons)
            ? []
            : _failureReasons.Split(
                ReasonSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// True when something needs the member rather than a retry.
    /// <para>
    /// A revoked grant needs them; a provider that was briefly down does not. Collapsing the two
    /// would put a "reconnect your calendar" banner in front of somebody whose calendar is fine.
    /// </para>
    /// </summary>
    public bool NeedsAttention { get; private set; }

    public static IntegrationSyncRun Start(
        IntegrationSyncRunId id,
        IntegrationConnectionId connectionId,
        Guid ownerId,
        string provider,
        string? calendarId,
        SyncDirection direction,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        return new IntegrationSyncRun(
            id,
            connectionId,
            ownerId,
            provider,
            string.IsNullOrWhiteSpace(calendarId)
                ? null
                : calendarId.Trim()[..Math.Min(calendarId.Trim().Length, CalendarIdLength)],
            direction,
            now);
    }

    /// <summary>Whether this run is still the live one at <paramref name="now"/>.</summary>
    public bool IsLive(DateTimeOffset now) =>
        Phase is SyncPhase.Syncing && now - StartedAt < StaleAfter;

    /// <summary>
    /// Closes the run with what actually happened.
    /// <para>
    /// The phase is derived, never supplied. A caller that could pass <see cref="SyncPhase.Synced"/>
    /// alongside a non-zero failure count is a caller that can report a success that did not happen,
    /// and this is the one place on the integration surface where that claim is made.
    /// </para>
    /// </summary>
    public void Complete(
        int itemsSynced, IReadOnlyList<string> failureReasons, bool needsAttention, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemsSynced);
        ArgumentNullException.ThrowIfNull(failureReasons);

        var reasons = failureReasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(reason => reason.Trim().Replace(ReasonSeparator, ' '))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        ItemsSynced = itemsSynced;
        ItemsFailed = reasons.Length;
        _failureReasons = Clip(string.Join(ReasonSeparator, reasons));
        NeedsAttention = needsAttention;
        CompletedAt = now;

        Phase = (itemsSynced, reasons.Length) switch
        {
            (_, 0) => SyncPhase.Synced,
            (0, _) => SyncPhase.Failed,
            _ => SyncPhase.Partial
        };
    }

    /// <summary>
    /// Closes a run that failed before it could count anything.
    /// <para>
    /// Distinct from <see cref="Complete"/> with a zero, because "the grant was rejected" and "the
    /// grant worked and twelve writes failed" are different conversations with a member.
    /// </para>
    /// </summary>
    public void Fail(string reason, bool needsAttention, DateTimeOffset now)
    {
        ItemsSynced = 0;
        ItemsFailed = 0;
        _failureReasons = Clip(reason?.Trim().Replace(ReasonSeparator, ' ') ?? string.Empty);
        NeedsAttention = needsAttention;
        Phase = SyncPhase.Failed;
        CompletedAt = now;
    }

    /// <summary>
    /// Closes a run nobody finished.
    /// <para>
    /// Called by the maintenance sweep, and it is <see cref="SyncPhase.Failed"/> rather than
    /// <see cref="SyncPhase.Partial"/>: nothing observed how far it got, so any count would be
    /// invented. <see cref="NeedsAttention"/> is false — a crashed worker is not something a member
    /// can act on.
    /// </para>
    /// </summary>
    public void Abandon(DateTimeOffset now)
    {
        if (Phase is not SyncPhase.Syncing)
        {
            return;
        }

        Phase = SyncPhase.Failed;
        NeedsAttention = false;
        _failureReasons = "This synchronisation stopped before it finished and its outcome is unknown.";
        CompletedAt = now;
    }

    private static string Clip(string value) =>
        value.Length <= FailureReasonsLength ? value : value[..FailureReasonsLength];
}
