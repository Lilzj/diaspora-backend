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
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>
/// Schedules an interview.
/// <para>
/// The round number comes from the highest already recorded, so two recruiters scheduling at once
/// cannot both create "round 2" — the unique index on (application, round) is the backstop that
/// makes the second one fail rather than silently duplicate.
/// </para>
/// <para>
/// The application is moved to <c>InterviewScheduled</c> in the same transaction. An interview
/// booked against an application still showing "submitted" is a candidate who has been told two
/// contradictory things.
/// </para>
/// </summary>
public sealed class ScheduleInterviewCommandHandler
    : ICommandHandler<ScheduleInterviewCommand, InterviewResponse>
{
    private readonly IInterviewRepository _interviews;
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ScheduleInterviewCommandHandler(
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
        ScheduleInterviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.GetAsync(
            JobApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<InterviewResponse>(JobErrors.ApplicationNotFound);
        }

        var employer = await _authorization.RequireEmployerAsync(
            application.EmployerId, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<InterviewResponse>(employer.Error);
        }

        if (ApplicationStatusTransitions.IsTerminal(application.Status))
        {
            return Result.Failure<InterviewResponse>(JobErrors.ApplicationAlreadyDecided);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var mode = JobWireNames.ParseInterviewMode(request.Mode);

        if (mode is null)
        {
            return Result.Failure<InterviewResponse>(JobErrors.InterviewLocationInvalid);
        }

        var schedule = InterviewSchedule.Create(
            request.ScheduledAt,
            request.DurationMinutes,
            mode.Value,
            now,
            request.TimeZoneId,
            request.LocationAddress,
            request.JoiningUrl,
            request.Instructions);

        if (schedule.IsFailure)
        {
            return Result.Failure<InterviewResponse>(schedule.Error);
        }

        var highestRound = await _interviews.GetHighestRoundAsync(
            application.Id, cancellationToken);

        if (highestRound >= JobLimits.MaxInterviewsPerApplication)
        {
            return Result.Failure<InterviewResponse>(JobErrors.InterviewLimit);
        }

        var interview = Interview.Arrange(
            InterviewId.From(_guids.NewSequentialGuid()),
            application.Id,
            application.PostingId,
            application.EmployerId,
            application.CandidateId,
            schedule.Value,
            highestRound + 1,
            request.Title,
            [.. request.Participants.Select(participant => new InterviewParticipantDraft
            {
                Name = participant.Name,
                Role = participant.Role,
                UserId = participant.UserId
            })],
            now,
            _authorization.CallerId);

        if (interview.IsFailure)
        {
            return Result.Failure<InterviewResponse>(interview.Error);
        }

        await _interviews.AddAsync(interview.Value, cancellationToken);

        // The application moves and its timeline records the appointment. A candidate must never
        // hold an interview against an application that says nobody has looked at them.
        if (application.Status != ApplicationStatus.InterviewScheduled)
        {
            var moved = application.MoveTo(
                ApplicationStatus.InterviewScheduled, now, _authorization.CallerId);

            if (moved.IsFailure)
            {
                return Result.Failure<InterviewResponse>(moved.Error);
            }
        }

        application.NoteInterviewScheduled(
            _guids.NewSequentialGuid(),
            schedule.Value.ScheduledAt,
            _authorization.CallerId,
            now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var posting = await _postings.GetForReadAsync(application.PostingId, cancellationToken);

        return Result.Success(InterviewMapper.ToResponse(
            interview.Value,
            posting?.Title.Value ?? string.Empty,
            employer.Value.Identity.DisplayName,
            forEmployer: true));
    }
}
