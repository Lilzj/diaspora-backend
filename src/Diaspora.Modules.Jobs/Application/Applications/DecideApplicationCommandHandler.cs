using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Moves an application and updates the employer's measured responsiveness.
/// <para>
/// <b>The response metric is recorded here, on the first move only.</b> That is what makes an
/// employer's published response time a measurement rather than a claim — and doing it inside the
/// same transaction as the decision means the two can never disagree.
/// </para>
/// </summary>
public sealed class DecideApplicationCommandHandler
    : ICommandHandler<DecideApplicationCommand, JobApplicationResponse>
{
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public DecideApplicationCommandHandler(
        IJobApplicationRepository applications,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _applications = applications;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<JobApplicationResponse>> Handle(
        DecideApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.GetAsync(
            JobApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<JobApplicationResponse>(JobErrors.ApplicationNotFound);
        }

        var employer = await _authorization.RequireEmployerAsync(
            application.EmployerId, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(employer.Error);
        }

        var target = JobWireNames.ParseApplicationStatus(command.Request.Status);

        if (target is null)
        {
            return Result.Failure<JobApplicationResponse>(
                JobErrors.ApplicationTransitionInvalid);
        }

        var now = _clock.UtcNow;
        var wasFirstResponse = application.TimeToFirstResponse is null;
        var submittedAt = application.SubmittedAt;

        var moved = application.MoveTo(
            target.Value,
            now,
            _authorization.CallerId,
            command.Request.Reason,
            command.Request.ReasonVisibleToCandidate);

        if (moved.IsFailure)
        {
            return Result.Failure<JobApplicationResponse>(moved.Error);
        }

        // Measured, not claimed. Only the first move counts as a response.
        if (wasFirstResponse)
        {
            employer.Value.RecordFirstResponse(now - submittedAt);
        }

        if (ApplicationStatusTransitions.IsTerminal(target.Value))
        {
            employer.Value.RecordDecision();
        }

        var posting = await _postings.GetAsync(application.PostingId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ApplicationMapper.ToResponse(
            application,
            posting?.Title.Value ?? string.Empty,
            posting?.Slug.Value ?? string.Empty,
            employer.Value.Identity.DisplayName,
            forEmployer: true,
            now));
    }
}
