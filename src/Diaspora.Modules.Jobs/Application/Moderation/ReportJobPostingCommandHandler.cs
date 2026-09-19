using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Moderation;

/// <summary>
/// Files a report about a job advert, and flags the advert when enough people agree.
/// <para>
/// <b>A demand for money flags on the first report.</b> Every other reason waits for the
/// threshold. Waiting for a second reporter is a reasonable defence against one disgruntled
/// applicant, and an unreasonable thing to ask of somebody who has just been told to pay for
/// their own visa processing — by then the money is gone and so is the advert.
/// </para>
/// <para>
/// Flagging does not hide anything. The advert stays up and a moderator looks at it, because
/// taking somebody's job advert down on two anonymous reports would make this queue a weapon.
/// </para>
/// </summary>
public sealed class ReportJobPostingCommandHandler : ICommandHandler<ReportJobPostingCommand, Guid>
{
    private readonly IJobReportRepository _reports;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ReportJobPostingCommandHandler(
        IJobReportRepository reports,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _reports = reports;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(
        ReportJobPostingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.PostingId), cancellationToken);

        // A 404 rather than a 403 on something the caller cannot see: a refusal would confirm
        // the advert exists, which is a probe against a hidden posting.
        if (posting is null || !await _authorization.CanSeeAsync(posting, cancellationToken))
        {
            return Result.Failure<Guid>(JobErrors.PostingNotFound);
        }

        // The report count is what flags an advert, so a seat at the employer that could report
        // its own would be a way to bury a rival's posting behind a flag.
        if (await _authorization.IsMemberOfAsync(posting.EmployerId, cancellationToken))
        {
            return Result.Failure<Guid>(JobErrors.CannotReportOwnPosting);
        }

        if (await _reports.HasReportedAsync(posting.Id, caller.Value, cancellationToken))
        {
            return Result.Failure<Guid>(JobErrors.AlreadyReported);
        }

        if (!Enum.TryParse<JobReportReason>(command.Request.Reason, ignoreCase: true, out var reason)
            || !Enum.IsDefined(reason))
        {
            return Result.Failure<Guid>(JobErrors.ReportReasonInvalid);
        }

        var now = _clock.UtcNow;

        var report = JobReport.File(
            JobReportId.From(_guids.NewSequentialGuid()),
            posting.Id,
            posting.EmployerId,
            caller.Value,
            reason,
            command.Request.Detail,
            now);

        if (report.IsFailure)
        {
            return Result.Failure<Guid>(report.Error);
        }

        await _reports.AddAsync(report.Value, cancellationToken);

        // Counted before this one is saved, so the new report is added rather than assumed.
        var total = await _reports.CountForPostingAsync(posting.Id, cancellationToken) + 1;

        if (report.Value.FlagsImmediately || total >= JobLimits.ReportsBeforeFlag)
        {
            // Flag, not hide. See the class summary.
            posting.Flag(flaggedBy: null, reason.ToString(), now);
        }

        report.Value.Announce(total, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(report.Value.Id.Value);
    }
}
