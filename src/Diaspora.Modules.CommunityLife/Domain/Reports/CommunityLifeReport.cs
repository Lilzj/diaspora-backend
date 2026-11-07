using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Reports;

/// <summary>
/// One member's report about an event or an organization.
/// <para>
/// <b>One aggregate for both subjects rather than two.</b> The moderation queue is one queue — a
/// moderator working through complaints does not want two screens — and the arithmetic that drives
/// auto-flagging is identical on both. <see cref="SubjectType"/> plus <see cref="SubjectId"/> is
/// the same content-reference shape B6 uses for exactly this reason.
/// </para>
/// <para>
/// <b>Never deleted and never soft-deleted.</b> A report is the record of a decision, and a
/// platform that could not show why it hid something has no answer for the organizer who asks. What
/// it does not carry into any published event is the reporter's identity.
/// </para>
/// <para>
/// <b><see cref="CommunityLifeReportReason.Duplicate"/> resolves differently from every other
/// reason.</b> A duplicate is not abuse — it is four committee members each posting the same
/// festival — and upholding it should merge rather than punish. That is why
/// <see cref="MergedIntoSubjectId"/> exists: the moderator names the surviving event, and the
/// decision is recorded rather than inferred from a hidden row.
/// </para>
/// </summary>
public sealed class CommunityLifeReport
    : AggregateRoot<CommunityLifeReportId>, IAuditableEntity
{
    private CommunityLifeReport(
        CommunityLifeReportId id,
        ReportSubjectType subjectType,
        Guid subjectId,
        UserId reportedByUserId,
        CommunityLifeReportReason reason,
        string? detail,
        DateTimeOffset createdAt) : base(id)
    {
        SubjectType = subjectType;
        SubjectId = subjectId;
        ReportedByUserId = reportedByUserId;
        Reason = reason;
        Detail = detail;
        Status = CommunityLifeReportStatus.Open;
        CreatedAt = createdAt;
    }

    private CommunityLifeReport() { }

    public ReportSubjectType SubjectType { get; private set; }

    /// <summary>The event or organization identifier, untyped so one table serves both.</summary>
    public Guid SubjectId { get; private set; }

    /// <summary>
    /// Who filed it. Read by the moderator and by nobody else — it never reaches a published event
    /// or any surface the reported party can see.
    /// </summary>
    public UserId ReportedByUserId { get; private set; } = default!;

    public CommunityLifeReportReason Reason { get; private set; }

    public string? Detail { get; private set; }

    public CommunityLifeReportStatus Status { get; private set; }

    public UserId? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    /// <summary>
    /// Where a duplicate was merged to. Set only when a duplicate report is upheld, and the reason
    /// this outcome is a decision rather than an inference.
    /// </summary>
    public Guid? MergedIntoSubjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsResolved => Status
        is CommunityLifeReportStatus.Upheld
        or CommunityLifeReportStatus.Dismissed
        or CommunityLifeReportStatus.Duplicate;

    /// <summary>
    /// Whether the reason is severe enough to flag on the first report.
    /// <para>
    /// Fraud and impersonation are, and nothing else is. Both are claims that somebody is being
    /// deceived right now, and waiting for a second complaint means waiting for a second victim.
    /// </para>
    /// </summary>
    public bool FlagsImmediately => Reason
        is CommunityLifeReportReason.Fraud
        or CommunityLifeReportReason.Impersonation;

    public static Result<CommunityLifeReport> File(
        CommunityLifeReportId id,
        ReportSubjectType subjectType,
        Guid subjectId,
        UserId reportedByUserId,
        CommunityLifeReportReason reason,
        string? detail,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(reportedByUserId);

        if (subjectId == Guid.Empty)
        {
            return Result.Failure<CommunityLifeReport>(
                subjectType == ReportSubjectType.Event
                    ? CommunityLifeErrors.EventNotFound
                    : CommunityLifeErrors.OrganizationNotFound);
        }

        var trimmed = detail?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxNoteLength })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxNoteLength];
        }

        return Result.Success(new CommunityLifeReport(
            id,
            subjectType,
            subjectId,
            reportedByUserId,
            reason,
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            createdAt));
    }

    /// <summary>
    /// Announces the report, carrying the running count rather than the reporter.
    /// <para>
    /// Separate from <see cref="File"/> because the count is not known until the row exists. Nothing
    /// raised this until 7 Nov 2026, so the handlers waiting for it never ran and no report reached
    /// Trust; the reporter is read from this row by the handler that sends it to the case, never
    /// carried on the event.
    /// </para>
    /// </summary>
    public void Announce(int totalReports, DateTimeOffset at) =>
        RaiseDomainEvent(new Events.CommunityLifeReportedDomainEvent
        {
            ReportId = Id,
            SubjectType = SubjectType,
            SubjectId = SubjectId,
            Reason = Reason,
            TotalReports = totalReports,
            ReportedAt = at
        });

    /// <summary>A moderator picks it up.</summary>
    public Result Claim(UserId moderatorId, DateTimeOffset now)
    {
        if (IsResolved)
        {
            return Result.Failure(CommunityLifeErrors.ReportAlreadyResolved);
        }

        Status = CommunityLifeReportStatus.UnderReview;
        ResolvedByUserId = moderatorId;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Records the decision on the report.
    /// <para>
    /// Since 7 Nov 2026 the decision is made on the Trust case and the report is closed from the
    /// decision event, by <c>CommunityLifeModerationActionTakenConsumer</c>. What happens to the
    /// subject is carried out separately by the module's moderation targets: hiding an event and
    /// closing a report are two writes against two aggregates.
    /// </para>
    /// </summary>
    public Result Resolve(
        CommunityLifeReportStatus outcome,
        UserId moderatorId,
        string? note,
        DateTimeOffset now,
        Guid? mergedIntoSubjectId = null)
    {
        if (IsResolved)
        {
            return Result.Failure(CommunityLifeErrors.ReportAlreadyResolved);
        }

        if (outcome is not (CommunityLifeReportStatus.Upheld
            or CommunityLifeReportStatus.Dismissed
            or CommunityLifeReportStatus.Duplicate))
        {
            return Result.Failure(CommunityLifeErrors.ReportAlreadyResolved);
        }

        var trimmed = note?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxModerationNote })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxModerationNote];
        }

        Status = outcome;
        ResolvedByUserId = moderatorId;
        ResolvedAt = now;
        ResolutionNote = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        ModifiedAt = now;

        if (outcome == CommunityLifeReportStatus.Upheld)
        {
            RaiseDomainEvent(new Events.CommunityLifeReportUpheldDomainEvent
            {
                ReportId = Id,
                SubjectType = SubjectType,
                SubjectId = SubjectId,
                ReportedByUserId = ReportedByUserId,
                UpheldAt = now
            });
        }

        if (mergedIntoSubjectId is { } target && target != Guid.Empty && target != SubjectId)
        {
            MergedIntoSubjectId = target;
        }

        return Result.Success();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
