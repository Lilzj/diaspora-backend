using System.Globalization;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Automation.Domain.Scheduling;

/// <summary>
/// One dated thing the platform will look at again before its moment arrives.
/// <para>
/// <b>Wave 12's one new store, and it exists because four frontend triggers are scheduled projections
/// over somebody else's date.</b> A job posting closes, a listing expires, a reservation ends, a
/// viewing happens. Each of those moments is published — <c>JobPublished.ClosingAt</c>,
/// <c>ListingPublished.ExpiresAt</c>, <c>ListingReserved.ExpiresAt</c>,
/// <c>ViewingScheduled.ScheduledFor</c> — and none of them is an event at the moment a member cares
/// about. A row here is the platform remembering to come back.
/// </para>
///
/// <para><b>One row per subject, not per automation, and that is the load-bearing decision.</b></para>
/// <para>
/// The obvious shape is an occurrence per (automation × subject), written when the publication
/// arrives. It is wrong twice over: at publication time the automations that will exist at the horizon
/// are not yet known — a member who writes the rule tomorrow would never be told — and a platform-wide
/// publication with ten thousand watchers would write ten thousand rows for one job. Fanning out at
/// <i>due</i> time instead means one row per subject, and the engine's own
/// <c>ListRunnableForTriggerAsync</c> does the matching it already does for every event-driven trigger.
/// </para>
///
/// <para><b>It is not a second scheduler, for the same reason the occurrence store is not.</b></para>
/// <para>
/// No cron registry, no job type, no handler dispatch, no timer. A durable due row with a claim
/// protocol, swept by the worker that already exists, under the B17 lease that already exists.
/// </para>
///
/// <para><b>The identity is <c>(TriggerKey, SubjectId)</c> and it is unique.</b></para>
/// <para>
/// A republished listing, a re-scheduled viewing and a redelivered message all name the same subject
/// under the same trigger. The unique index is what makes the second one an <i>update</i> to a horizon
/// that moved rather than a second notification about the same thing — and it is a database
/// constraint rather than a read, because two instances consuming a redelivered message would both
/// pass a read.
/// </para>
/// </summary>
public sealed class AutomationSubjectHorizon : AggregateRoot<AutomationSubjectHorizonId>
{
    private AutomationSubjectHorizon() { }

    private AutomationSubjectHorizon(AutomationSubjectHorizonId id) : base(id) { }

    /// <summary>The catalogue key this will fire — <c>job.closingSoon</c>.</summary>
    public string TriggerKey { get; private set; } = string.Empty;

    /// <summary>What kind of thing it is, from <c>AutomationSubjectKinds</c>.</summary>
    public string SubjectKind { get; private set; } = string.Empty;

    public Guid SubjectId { get; private set; }

    /// <summary>The subject's own title, captured so the notification reads correctly after a rename.</summary>
    public string SubjectTitle { get; private set; } = string.Empty;

    /// <summary>The subject's own moment — when the job closes, when the viewing starts.</summary>
    public DateTimeOffset SubjectMoment { get; private set; }

    /// <summary>The subject's own zone. Never the device's — see the reminder aggregate's note.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>
    /// When the platform comes back, which is <see cref="SubjectMoment"/> less the lead time.
    /// <para>
    /// Stored rather than computed at read time so a lead-time configuration change does not silently
    /// re-date every horizon already written, and so the sweep's index is on a column.
    /// </para>
    /// </summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>
    /// Who this concerns, as a delimited list of identifiers, or empty for everybody.
    /// <para>
    /// Delimited rather than an owned collection: this module has no owned type anywhere, deliberately,
    /// and this list holds at most three identifiers — a profile id and the user behind it.
    /// </para>
    /// <para>
    /// <b>Empty means "anybody watching", and that is the platform's established reading.</b> Wave 11's
    /// <c>event.created</c>, <c>guide.published</c> and <c>guide.savedChanged</c> consumers all pass an
    /// empty owner set for the same reason: a new job closing concerns everybody who wrote a rule about
    /// jobs closing, and the member's own conditions narrow it. The alternative — resolving who saved
    /// the job — would mean this module reading the Bookmarks module's store, which is exactly the
    /// cross-module read the whole arrangement exists without.
    /// </para>
    /// </summary>
    public string ConcernedOwnerIds { get; private set; } = string.Empty;

    /// <summary>The condition values this subject can be checked against, serialized.</summary>
    public string Values { get; private set; } = string.Empty;

    /// <summary>What the member is told, in their words. Public subject fields only.</summary>
    public string Summary { get; private set; } = string.Empty;

    public AutomationOccurrenceStatus Status { get; private set; }

    public string? ClaimedBy { get; private set; }

    public DateTimeOffset? ClaimExpiresAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Why it was cancelled or skipped, for an operator reading the row later.</summary>
    public string? Resolution { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int Revision { get; private set; }

    /// <summary>The unique identity, as the database sees it.</summary>
    public static string BuildKey(string triggerKey, Guid subjectId) =>
        $"{triggerKey}:{subjectId:N}";

    public string DeduplicationKey => BuildKey(TriggerKey, SubjectId);

    public static AutomationSubjectHorizon Schedule(
        string triggerKey,
        string subjectKind,
        Guid subjectId,
        string subjectTitle,
        DateTimeOffset subjectMoment,
        DateTimeOffset dueAt,
        string timeZoneId,
        IReadOnlyList<Guid> concernedOwnerIds,
        string values,
        string summary,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(concernedOwnerIds);

        return new AutomationSubjectHorizon(AutomationSubjectHorizonId.New())
        {
            TriggerKey = triggerKey,
            SubjectKind = subjectKind,
            SubjectId = subjectId,
            SubjectTitle = Cap(subjectTitle, 200) ?? string.Empty,
            SubjectMoment = subjectMoment,
            DueAt = dueAt,
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId,
            ConcernedOwnerIds = Join(concernedOwnerIds),
            Values = values,
            Summary = Cap(summary, 300) ?? string.Empty,
            Status = AutomationOccurrenceStatus.Pending,
            CreatedAt = now,
            Revision = 1
        };
    }

    /// <summary>
    /// The subject's date moved, so the horizon moves with it.
    /// <para>
    /// <b>A horizon that already fired is not re-armed.</b> A republished listing whose expiry did not
    /// change must not produce a second "about to expire" — that is the same notification twice, and it
    /// is the most likely way a redelivered message becomes a duplicate here. Only a pending row is
    /// rebased; a completed one stays completed, and the member hears once.
    /// </para>
    /// </summary>
    public bool Rebase(
        DateTimeOffset subjectMoment,
        DateTimeOffset dueAt,
        string subjectTitle,
        string summary,
        string values)
    {
        if (Status is not (AutomationOccurrenceStatus.Pending or AutomationOccurrenceStatus.Claimed))
        {
            return false;
        }

        SubjectMoment = subjectMoment;
        DueAt = dueAt;
        SubjectTitle = Cap(subjectTitle, 200) ?? SubjectTitle;
        Summary = Cap(summary, 300) ?? Summary;
        Values = values;
        Status = AutomationOccurrenceStatus.Pending;
        ClaimedBy = null;
        ClaimExpiresAt = null;
        CompletedAt = null;
        Resolution = null;
        Revision++;
        return true;
    }

    /// <summary>Takes the horizon for this instance. False when somebody holds an unexpired claim.</summary>
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

    /// <summary>Too late to be worth firing. The same catch-up policy the occurrence store applies.</summary>
    public void Skip(string reason, DateTimeOffset now)
    {
        Status = AutomationOccurrenceStatus.Skipped;
        Resolution = Cap(reason, 200);
        CompletedAt = now;
        ClaimedBy = null;
        ClaimExpiresAt = null;
        Revision++;
    }

    /// <summary>
    /// The subject went away, so the horizon does too.
    /// <para>
    /// A job closed early, a listing sold, a reservation was cancelled, a viewing was called off. Every
    /// one of those is a published fact, and cancelling here is what stops the platform reminding a
    /// member about something that is no longer going to happen.
    /// </para>
    /// </summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status is AutomationOccurrenceStatus.Completed or AutomationOccurrenceStatus.Cancelled)
        {
            return;
        }

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

    /// <summary>The owner set, parsed back. Empty means anybody.</summary>
    public IReadOnlyList<Guid> Owners() =>
        string.IsNullOrWhiteSpace(ConcernedOwnerIds)
            ? []
            : [.. ConcernedOwnerIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Guid.TryParse(part, CultureInfo.InvariantCulture, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)];

    private static string Join(IReadOnlyList<Guid> ids) =>
        string.Join(',', ids.Where(id => id != Guid.Empty).Distinct().Select(id => id.ToString("D")));

    private static string? Cap(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value : value[..max];
}
