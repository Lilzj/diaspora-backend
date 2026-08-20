using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>
/// Records an interview outcome.
/// <para>
/// <b>The outcome does not decide the application.</b> Recording "passed" moves the application
/// to <c>InterviewCompleted</c> and stops there — whether that becomes an offer or a rejection is
/// a separate, deliberate decision a person makes. Automating it would turn one interviewer's
/// opinion into a hiring decision nobody signed off, which is exactly what automated interview
/// scoring is out of scope to prevent.
/// </para>
/// </summary>
public sealed class RecordInterviewOutcomeCommandHandler
    : ICommandHandler<RecordInterviewOutcomeCommand, InterviewResponse>
{
    private readonly IInterviewRepository _interviews;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RecordInterviewOutcomeCommandHandler(
        IInterviewRepository interviews,
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _interviews = interviews;
        _applications = applications;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<InterviewResponse>> Handle(
        RecordInterviewOutcomeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var interview = await _interviews.GetAsync(
            InterviewId.From(command.InterviewId), cancellationToken);

        if (interview is null)
        {
            return Result.Failure<InterviewResponse>(JobErrors.InterviewNotFound);
        }

        var employer = await _authorization.RequireEmployerAsync(
            interview.EmployerId, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<InterviewResponse>(employer.Error);
        }

        var outcome = JobWireNames.ParseInterviewOutcome(command.Request.Outcome);

        if (outcome is null)
        {
            return Result.Failure<InterviewResponse>(JobErrors.InterviewNotScheduled);
        }

        var now = _clock.UtcNow;

        var recorded = interview.RecordOutcome(
            outcome.Value,
            command.Request.Feedback,
            command.Request.CandidateFeedback,
            now,
            _authorization.CallerId);

        if (recorded.IsFailure)
        {
            return Result.Failure<InterviewResponse>(recorded.Error);
        }

        var application = await _applications.GetAsync(
            interview.ApplicationId, cancellationToken);

        if (application is not null)
        {
            application.NoteInterviewCompleted(
                _guids.NewSequentialGuid(), outcome.Value, _authorization.CallerId, now);

            // Moves to InterviewCompleted and stops. What happens next is a person's decision.
            if (application.Status == ApplicationStatus.InterviewScheduled)
            {
                application.MoveTo(
                    ApplicationStatus.InterviewCompleted, now, _authorization.CallerId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var posting = await _postings.GetForReadAsync(interview.PostingId, cancellationToken);

        return Result.Success(InterviewMapper.ToResponse(
            interview,
            posting?.Title.Value ?? string.Empty,
            employer.Value.Identity.DisplayName,
            forEmployer: true));
    }
}
