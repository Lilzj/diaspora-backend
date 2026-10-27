using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Reports;

/// <summary>
/// Somebody saying a job advert is not what it claims to be.
/// <para>
/// <b>Every other vertical on this platform has had this and jobs did not.</b> Housing,
/// marketplace, guides, community and community life could all be reported and moderated;
/// a job advert could not. The permissions existed — <c>jobs:report</c> and <c>jobs:moderate</c>
/// were declared and gated nothing — and the posting aggregate has carried <c>Hide</c>,
/// <c>Flag</c> and its moderation trail since it was written, with no command able to reach
/// them. The gap was the report: nothing produced the thing a moderator would act on.
/// </para>
/// <para>
/// <b><see cref="JobReportReason.Scam"/> and <see cref="JobReportReason.FeeRequested"/> flag the
/// posting on the first report.</b> Every other reason waits for a second. Recruitment fraud is
/// what this queue exists for: an advert asking a candidate to pay for a visa, a course or a
/// background check takes the money of somebody who has just arrived, has no income yet and has
/// nobody to ask whether that is normal — and waiting for a second reporter means letting it run
/// another day against people in exactly that position.
/// </para>
/// <para>
/// <b>The reporter's identity never leaves this row.</b> It is not on the domain event, not on
/// any published contract and not on anything an employer can reach. Somebody who reports a
/// fraudulent employer must never become identifiable to them — they may be applying for other
/// jobs in the same small industry, in a country where they have no standing yet.
/// </para>
/// <para>
/// Never soft-deleted. A report is the record of a decision, and an appeal or an audit is
/// answered from the trail of what was reported and what was done about it.
/// </para>
/// </summary>
public sealed class JobReport : AggregateRoot<JobReportId>, IAuditableEntity
{
    private JobReport(
        JobReportId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        UserId reportedBy,
        JobReportReason reason,
        string? detail,
        DateTimeOffset createdAt) : base(id)
    {
        PostingId = postingId;
        EmployerId = employerId;
        ReportedBy = reportedBy;
        Reason = reason;
        Detail = detail;
        Status = JobReportStatus.Open;
        CreatedAt = createdAt;
    }

    private JobReport() { }

    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>
    /// Denormalised from the posting, so a moderator can ask "what else has this employer had
    /// reported" without joining through every advert they have ever placed. An employer with
    /// three upheld fee demands is a different proposition from three separate one-offs, and
    /// that pattern is the whole signal in recruitment fraud.
    /// </summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    /// <summary>Who reported it. Never published — see the class summary.</summary>
    public UserId ReportedBy { get; private set; } = default!;

    public JobReportReason Reason { get; private set; }

    /// <summary>What they said, in their words. Read by a moderator, nobody else.</summary>
    public string? Detail { get; private set; }

    public JobReportStatus Status { get; private set; }

    public UserId? ResolvedBy { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>What the moderator decided. For the trail, never sent to the reporter verbatim.</summary>
    public string? Resolution { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether this report is still waiting on somebody.</summary>
    public bool IsOpen => Status is JobReportStatus.Open or JobReportStatus.UnderReview;

    /// <summary>
    /// Whether this report alone should flag the posting.
    /// <para>
    /// True where money is being asked of a candidate, false for everything else. The threshold
    /// for the rest is a configured number the handler applies.
    /// </para>
    /// </summary>
    public bool FlagsImmediately =>
        Reason is JobReportReason.Scam or JobReportReason.FeeRequested;

    public static Result<JobReport> File(
        JobReportId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        UserId reportedBy,
        JobReportReason reason,
        string? detail,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);
        ArgumentNullException.ThrowIfNull(reportedBy);

        var trimmed = detail?.Trim();

        if (trimmed is { Length: > JobLimits.MaxReportDetail })
        {
            trimmed = trimmed[..JobLimits.MaxReportDetail];
        }

        return Result.Success(new JobReport(
            id,
            postingId,
            employerId,
            reportedBy,
            reason,
            string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            createdAt));
    }

    /// <summary>
    /// Announces the report on the bus, carrying the running count rather than the reporter.
    /// <para>
    /// Separate from <see cref="File"/> because the count is not known until the row exists:
    /// the number that matters to whatever subscribes is how many distinct members have now
    /// said something is wrong, and that is a question about the table, not about this row.
    /// </para>
    /// </summary>
    public void Announce(int totalReports, DateTimeOffset at) =>
        RaiseDomainEvent(new Events.JobPostingReportedDomainEvent
        {
            ReportId = Id,
            PostingId = PostingId,
            EmployerId = EmployerId,
            Reason = Reason,
            TotalReports = totalReports,
            ReportedAt = at
        });

    /// <summary>A moderator has picked it up, so a queue can show who is on it.</summary>
    public Result TakeUp(UserId moderatorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (!IsOpen)
        {
            return Result.Failure(JobErrors.ReportAlreadyResolved);
        }

        Status = JobReportStatus.UnderReview;
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>
    /// Closes the report.
    /// <para>
    /// Upholding and dismissing are both closures and are recorded distinctly. An employer with
    /// three upheld reports is a different proposition from one with three dismissed ones, and a
    /// queue that recorded only "closed" would throw away the signal it exists to produce.
    /// </para>
    /// </summary>
    public Result Resolve(UserId moderatorId, bool upheld, string? resolution, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (!IsOpen)
        {
            return Result.Failure(JobErrors.ReportAlreadyResolved);
        }

        var trimmed = resolution?.Trim();

        if (trimmed is { Length: > JobLimits.MaxModerationNote })
        {
            trimmed = trimmed[..JobLimits.MaxModerationNote];
        }

        Status = upheld ? JobReportStatus.Upheld : JobReportStatus.Dismissed;
        ResolvedBy = moderatorId;
        ResolvedAt = now;
        Resolution = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        ModifiedAt = now;

        if (upheld)
        {
            RaiseDomainEvent(new Events.JobReportUpheldDomainEvent
            {
                ReportId = Id,
                PostingId = PostingId,
                EmployerId = EmployerId,
                ReportedBy = ReportedBy,
                UpheldAt = now
            });
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
