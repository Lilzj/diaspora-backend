using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Moderation;

/// <summary>
/// Reads the moderation queue.
/// <para>
/// Each row carries what a decision actually needs: the advert's title, the employer's name,
/// how many people have reported this advert, and <b>how many upheld reports that employer
/// already has across every advert they have placed</b>. The last of those is the only way a
/// moderator looking at one card can tell a one-off complaint from a pattern, which is what
/// recruitment fraud looks like from the inside.
/// </para>
/// </summary>
public sealed class ListJobReportsQueryHandler
    : IQueryHandler<ListJobReportsQuery, IReadOnlyList<JobReportResponse>>
{
    private const int DefaultLimit = 50;

    private const int MaximumLimit = 200;

    private readonly IJobReportRepository _reports;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly JobAuthorizationService _authorization;

    public ListJobReportsQueryHandler(
        IJobReportRepository reports,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        JobAuthorizationService authorization)
    {
        _reports = reports;
        _postings = postings;
        _employers = employers;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<JobReportResponse>>> Handle(
        ListJobReportsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<IReadOnlyList<JobReportResponse>>(JobErrors.NotModerator);
        }

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaximumLimit);

        var reports = await _reports.ListAsync(
            request.OpenOnly,
            request.FraudFirst,
            request.PostingId is { } postingId ? JobPostingId.From(postingId) : null,
            limit,
            cancellationToken);

        var rows = new List<JobReportResponse>(reports.Count);

        foreach (var report in reports)
        {
            var posting = await _postings.GetIncludingDeletedAsync(report.PostingId, cancellationToken);
            var employer = await _employers.GetForReadAsync(report.EmployerId, cancellationToken);

            rows.Add(new JobReportResponse
            {
                ReportId = report.Id.Value,
                PostingId = report.PostingId.Value,

                // A report outlives the advert it was about, so a removed posting reads as
                // removed rather than leaving the row blank.
                PostingTitle = posting?.Title.Value ?? "This advert has been removed",
                EmployerId = report.EmployerId.Value,
                EmployerName = employer?.Identity.DisplayName ?? "Unknown employer",
                Reason = report.Reason.ToString(),
                Detail = report.Detail,
                Status = report.Status.ToString(),
                EmployerUpheldReports = await _reports.CountUpheldForEmployerAsync(
                    report.EmployerId, cancellationToken),
                TotalReportsOnPosting = await _reports.CountForPostingAsync(
                    report.PostingId, cancellationToken),
                PostingModerationState = posting?.ModerationState.ToString() ?? "Unknown",
                Resolution = report.Resolution,
                ResolvedAt = report.ResolvedAt,
                CreatedAt = report.CreatedAt
            });
        }

        return Result.Success<IReadOnlyList<JobReportResponse>>(rows);
    }
}

/// <summary>
/// Closes a report, and every other open report about the same advert with it.
/// <para>
/// <b>Resolving one and leaving four open would put the same advert back at the top of the queue
/// four more times.</b> A moderator looks at an advert, not at a report, so the decision applies
/// to every allegation outstanding against it and each reporter's row records the same outcome.
/// </para>
/// <para>
/// Dismissing every report clears an automatic flag: the advert was looked at and found to be
/// nothing, and leaving it flagged would quietly penalise an employer who did nothing wrong.
/// Upholding leaves the advert exactly where the moderator's own action put it.
/// </para>
/// </summary>
public sealed class ResolveJobReportCommandHandler
    : ICommandHandler<ResolveJobReportCommand, JobReportResponse>
{
    private readonly IJobReportRepository _reports;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public ResolveJobReportCommandHandler(
        IJobReportRepository reports,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _reports = reports;
        _postings = postings;
        _employers = employers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<JobReportResponse>> Handle(
        ResolveJobReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<JobReportResponse>(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure<JobReportResponse>(JobErrors.NotModerator);
        }

        var report = await _reports.GetAsync(
            JobReportId.From(command.ReportId), cancellationToken);

        if (report is null)
        {
            return Result.Failure<JobReportResponse>(JobErrors.ReportNotFound);
        }

        if (!report.IsOpen)
        {
            return Result.Failure<JobReportResponse>(JobErrors.ReportAlreadyResolved);
        }

        var now = _clock.UtcNow;
        var upheld = command.Request.Upheld;
        var resolution = command.Request.Resolution;

        // Every open allegation about this advert, decided together. See the class summary.
        var siblings = await _reports.GetOpenForPostingAsync(report.PostingId, cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.Resolve(caller.Value, upheld, resolution, now);
        }

        // The report may not be among the siblings if it was already under review by another
        // path; resolving it twice is refused by the aggregate, so this is safe either way.
        if (siblings.All(entry => entry.Id != report.Id))
        {
            report.Resolve(caller.Value, upheld, resolution, now);
        }

        var posting = await _postings.GetAsync(report.PostingId, cancellationToken);

        if (posting is not null)
        {
            if (upheld && command.Request.HidePosting)
            {
                var hidden = posting.Hide(
                    resolution ?? "Report upheld.", caller.Value, now);

                if (hidden.IsFailure)
                {
                    return Result.Failure<JobReportResponse>(hidden.Error);
                }
            }
            else if (!upheld)
            {
                // Found to be nothing. Back into discovery, from a flag or a hide alike.
                posting.Clear(caller.Value, now);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var employer = await _employers.GetForReadAsync(report.EmployerId, cancellationToken);

        return Result.Success(new JobReportResponse
        {
            ReportId = report.Id.Value,
            PostingId = report.PostingId.Value,
            PostingTitle = posting?.Title.Value ?? "This advert has been removed",
            EmployerId = report.EmployerId.Value,
            EmployerName = employer?.Identity.DisplayName ?? "Unknown employer",
            Reason = report.Reason.ToString(),
            Detail = report.Detail,
            Status = report.Status.ToString(),
            EmployerUpheldReports = await _reports.CountUpheldForEmployerAsync(
                report.EmployerId, cancellationToken),
            TotalReportsOnPosting = await _reports.CountForPostingAsync(
                report.PostingId, cancellationToken),
            PostingModerationState = posting?.ModerationState.ToString() ?? "Unknown",
            Resolution = report.Resolution,
            ResolvedAt = report.ResolvedAt,
            CreatedAt = report.CreatedAt
        });
    }
}

/// <summary>
/// Acts on the advert itself, separately from closing any report.
/// <para>
/// The verbs are the aggregate's own: hiding takes a reason and lifting a hide does not, because
/// friction belongs on the restricting side of a moderation decision and never on the side that
/// undoes it.
/// </para>
/// </summary>
public sealed class ModerateJobPostingCommandHandler : ICommandHandler<ModerateJobPostingCommand>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ModerateJobPostingCommandHandler(
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ModerateJobPostingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure(JobErrors.NotModerator);
        }

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.PostingId), cancellationToken);

        if (posting is null)
        {
            return Result.Failure(JobErrors.PostingNotFound);
        }

        var now = _clock.UtcNow;
        var reason = command.Request.Reason;

        var applied = command.Request.Action?.Trim().ToLowerInvariant() switch
        {
            "hide" => posting.Hide(reason, caller.Value, now),
            "unhide" => posting.Unhide(caller.Value, now),
            "flag" => posting.Flag(caller.Value, reason, now),
            "note" => posting.AddModerationNote(
                _guids.NewSequentialGuid(), caller.Value, reason, now),

            // An action this module cannot read is refused rather than ignored: answering a word
            // it does not know with success is how a moderator comes to believe they hid an
            // advert they did not.
            _ => Result.Failure(JobErrors.ModerationNoteInvalid)
        };

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
