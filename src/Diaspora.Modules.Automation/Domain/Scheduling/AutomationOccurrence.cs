using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Scheduling;

/// <summary>
/// One moment a time-based automation is due.
/// <para>
/// <b>A row rather than a timer, and that is the whole of GAP-003.</b> B17's shipped scheduler says of
/// itself that "jobs do not survive a restart" and that "anything that must happen goes through the
/// outbox, which is durable". A member's 07:45 reminder must happen, so it is persisted, claimed by
/// exactly one instance under a lease, and completed or released — none of which a dictionary of
/// callbacks can promise across a deployment.
/// </para>
/// <para>
/// <b>This is not a second scheduler.</b> It owns no cron registry, no job types and no handler
/// dispatch. It is a due-store with a claim protocol, which is precisely the durable half B17's
/// in-memory implementation documents itself as lacking.
/// </para>
/// <para>
/// <b>The identity is <c>(AutomationId, DueAt)</c>, and it is unique.</b> A scheduler that fires twice
/// for one occurrence — a restart mid-dispatch, a clock adjustment, two instances racing — is the
/// single most likely duplicate-notification source in the whole feature, because a timer is retried
/// by infrastructure the automation platform does not control. The index is what makes the second
/// insert fail rather than the second notification arrive.
/// </para>
/// </summary>
public sealed class AutomationOccurrence : AggregateRoot<AutomationOccurrenceId>
{
    private AutomationOccurrence() { }

    private AutomationOccurrence(AutomationOccurrenceId id) : base(id) { }

    public AutomationId AutomationId { get; private set; } = null!;

    public Guid OwnerId { get; private set; }

    /// <summary>The absolute instant. Computed from the wall clock and the IANA zone, once, here.</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>
    /// The wall clock the member actually chose, and the zone it was chosen in.
    /// <para>
    /// Kept beside the instant rather than derived from it, because a zone whose rules change between
    /// the write and the sweep would otherwise silently reinterpret what the member asked for. It is
    /// also what a support conversation needs: "you asked for 09:00 Africa/Lagos and we ran at 08:00
    /// UTC" is answerable; "we ran at 08:00 UTC" is not.
    /// </para>
    /// </summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public string LocalWallClock { get; private set; } = string.Empty;

    public AutomationOccurrenceStatus Status { get; private set; }

    /// <summary>Which instance holds it, and until when. Null when unclaimed.</summary>
    public string? ClaimedBy { get; private set; }

    public DateTimeOffset? ClaimExpiresAt { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>The last failure's message. Never a stack trace.</summary>
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Why it was skipped or cancelled, for the run record that stands in its place.</summary>
    public string? Resolution { get; private set; }

    public int Revision { get; private set; }

    public static AutomationOccurrence Schedule(
        AutomationId automationId,
        Guid ownerId,
        DateTimeOffset dueAt,
        string timeZoneId,
        string localWallClock,
        DateTimeOffset now) =>
        new(AutomationOccurrenceId.New())
        {
            AutomationId = automationId,
            OwnerId = ownerId,
            DueAt = dueAt,
            TimeZoneId = timeZoneId,
            LocalWallClock = localWallClock,
            Status = AutomationOccurrenceStatus.Pending,
            CreatedAt = now,
            Revision = 1
        };

    /// <summary>
    /// Takes the occurrence for this instance.
    /// <para>
    /// Returns false when somebody else holds an unexpired claim. The caller skips it rather than
    /// waiting: by the time it acquired, the work would already be done.
    /// </para>
    /// </summary>
    public bool TryClaim(string owner, DateTimeOffset now, TimeSpan claimFor)
    {
        if (Status is not (AutomationOccurrenceStatus.Pending or AutomationOccurrenceStatus.Claimed))
        {
            return false;
        }

        if (Status == AutomationOccurrenceStatus.Claimed &&
            ClaimExpiresAt is { } expiry &&
            expiry > now)
        {
            return false;
        }

        Status = AutomationOccurrenceStatus.Claimed;
        ClaimedBy = owner;
        ClaimExpiresAt = now.Add(claimFor);
        Attempts++;
        Revision++;
        return true;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = AutomationOccurrenceStatus.Completed;
        CompletedAt = now;
        ClaimedBy = null;
        ClaimExpiresAt = null;
        Revision++;
    }

    /// <summary>
    /// Too late to be worth firing.
    /// <para>
    /// The catch-up policy, applied. An occurrence more than one interval late is dropped with a
    /// record rather than fired: a daily reminder that arrives eleven hours late is worse than one
    /// that does not arrive, and history has to show it was skipped and why.
    /// </para>
    /// </summary>
    public void Skip(string reason, DateTimeOffset now)
    {
        Status = AutomationOccurrenceStatus.Skipped;
        Resolution = Cap(reason, 200);
        CompletedAt = now;
        ClaimedBy = null;
        ClaimExpiresAt = null;
        Revision++;
    }

    /// <summary>Cancelled by a pause, a delete, or a subject that moved.</summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        Status = AutomationOccurrenceStatus.Cancelled;
        Resolution = Cap(reason, 200);
        CompletedAt = now;
        ClaimedBy = null;
        ClaimExpiresAt = null;
        Revision++;
    }

    public void RecordFailure(string error, int maximumAttempts, DateTimeOffset now)
    {
        LastError = Cap(error, 300);

        if (Attempts >= maximumAttempts)
        {
            // Dead-lettered rather than dropped. A due moment that failed enough times is kept so an
            // operator can see it; silently discarding it is how a member's reminder disappears with
            // nobody able to say what happened.
            Status = AutomationOccurrenceStatus.DeadLettered;
            CompletedAt = now;
        }
        else
        {
            Status = AutomationOccurrenceStatus.Pending;
        }

        ClaimedBy = null;
        ClaimExpiresAt = null;
        Revision++;
    }

    private static string? Cap(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}

/// <summary>
/// One member's reminder on one subject.
/// <para>
/// <b>The five states stay distinct and are never collapsed.</b> <c>Configured</c> is what the member
/// did; <c>Scheduled</c> is what the platform accepted. A UI that showed a tick for the first would
/// be claiming the second.
/// </para>
/// <para>
/// <b>The offset is computed in the subject's own zone, not the device's.</b> "One day before" an
/// event at 18:00 <c>Africa/Lagos</c> is 18:00 Lagos time the previous day, and it stays so if the
/// member flies to London. The zone is stored because re-deriving it later from a device would give a
/// different answer.
/// </para>
/// </summary>
public sealed class AutomationReminder : AggregateRoot<AutomationReminderId>
{
    private AutomationReminder() { }

    private AutomationReminder(AutomationReminderId id) : base(id) { }

    public UserId MemberId { get; private set; } = UserId.Empty;

    /// <summary><c>event</c>, <c>occurrence</c> or <c>series</c>.</summary>
    public string SubjectKind { get; private set; } = string.Empty;

    public Guid SubjectId { get; private set; }

    /// <summary>The subject's own title, captured so a reminder reads correctly after a rename.</summary>
    public string SubjectTitle { get; private set; } = string.Empty;

    /// <summary>The subject's own moment — the start, or the end.</summary>
    public DateTimeOffset SubjectMoment { get; private set; }

    /// <summary>Positive means before.</summary>
    public int OffsetMinutes { get; private set; }

    /// <summary><c>start</c> or <c>end</c>.</summary>
    public string Anchor { get; private set; } = "start";

    public string TimeZoneId { get; private set; } = string.Empty;

    public AutomationReminderState State { get; private set; }

    /// <summary>The absolute instant this is due. Null while the state is <c>Configured</c>.</summary>
    public DateTimeOffset? ScheduledFor { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// The mandatory key on <c>(member, subject, offset, anchor)</c>.
    /// <para>
    /// Tapping "remind me" twice must produce one reminder. A duplicate here is two identical pushes a
    /// minute apart, which is the most common way an automation feature loses a member's trust.
    /// </para>
    /// </summary>
    public string DeduplicationKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public int Attempts { get; private set; }

    public int Revision { get; private set; }

    public static AutomationReminder Configure(
        UserId memberId,
        string subjectKind,
        Guid subjectId,
        string subjectTitle,
        DateTimeOffset subjectMoment,
        int offsetMinutes,
        string anchor,
        string timeZoneId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        var reminder = new AutomationReminder(AutomationReminderId.New())
        {
            MemberId = memberId,
            SubjectKind = subjectKind,
            SubjectId = subjectId,
            SubjectTitle = subjectTitle.Length <= 200 ? subjectTitle : subjectTitle[..200],
            SubjectMoment = subjectMoment,
            OffsetMinutes = offsetMinutes,
            Anchor = anchor,
            TimeZoneId = timeZoneId,
            State = AutomationReminderState.Configured,
            CreatedAt = now,
            Revision = 1
        };

        reminder.DeduplicationKey = BuildKey(memberId, subjectKind, subjectId, offsetMinutes, anchor);
        return reminder;
    }

    public static string BuildKey(
        UserId memberId, string subjectKind, Guid subjectId, int offsetMinutes, string anchor) =>
        $"{memberId.Value:N}:{subjectKind}:{subjectId:N}:{offsetMinutes}:{anchor}";

    /// <summary>The platform accepted it and knows when it is due.</summary>
    public void MarkScheduled(DateTimeOffset scheduledFor)
    {
        State = AutomationReminderState.Scheduled;
        ScheduledFor = scheduledFor;
        Revision++;
    }

    public void MarkSent(DateTimeOffset sentAt)
    {
        State = AutomationReminderState.Sent;
        SentAt = sentAt;
        Revision++;
    }

    public void MarkFailed(string reason)
    {
        Attempts++;
        FailureReason = Cap(reason);

        // One retry, and then it stops. A reminder is time-sensitive: past the moment it was about,
        // a late one is worse than none, so there is no exponential backoff to exhaust.
        if (Attempts >= 2)
        {
            State = AutomationReminderState.Failed;
        }

        Revision++;
    }

    /// <summary>
    /// Gives up immediately, without a retry.
    /// <para>
    /// For the one case where retrying is certainly wrong: the moment the reminder was about has
    /// already passed. A second attempt would deliver it later still.
    /// </para>
    /// </summary>
    public void Abandon(string reason)
    {
        Attempts++;
        FailureReason = Cap(reason);
        State = AutomationReminderState.Failed;
        Revision++;
    }

    private static string Cap(string value) => value.Length <= 300 ? value : value[..300];

    public void Cancel()
    {
        State = AutomationReminderState.Cancelled;
        ScheduledFor = null;
        Revision++;
    }

    /// <summary>
    /// Moves the reminder because the subject moved.
    /// <para>
    /// An event rescheduled from Friday to Saturday must cancel the Friday reminder rather than fire
    /// it, and re-derive Saturday's. Doing it in one call keeps the two halves of that from diverging.
    /// </para>
    /// </summary>
    public void Reschedule(DateTimeOffset newSubjectMoment, DateTimeOffset newScheduledFor)
    {
        SubjectMoment = newSubjectMoment;
        ScheduledFor = newScheduledFor;
        State = AutomationReminderState.Scheduled;
        FailureReason = null;
        Revision++;
    }

    public bool IsPending => State is AutomationReminderState.Configured
        or AutomationReminderState.Scheduled;
}
