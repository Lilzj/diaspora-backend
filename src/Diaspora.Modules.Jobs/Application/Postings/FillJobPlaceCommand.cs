using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// The poster counts one place off: "1 place filled" (owner's request, 4 Nov 2026). On a simple
/// job people are taken in a conversation, not through an offer, so the poster says when a place
/// has gone, and the last one fills the job. The same permission as closing it, for the same
/// reason: a place that no longer exists wastes the afternoon of whoever answers it.
/// </summary>
[Authorize(Permission = JobPermissions.PostClose)]
[Auditable]
public sealed record FillJobPlaceCommand : ICommand<JobResponse>
{
    public required Guid JobId { get; init; }
}

public sealed class FillJobPlaceCommandHandler : ICommandHandler<FillJobPlaceCommand, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public FillJobPlaceCommandHandler(
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IClock clock)
    {
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<JobResponse>> Handle(
        FillJobPlaceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.JobId), cancellationToken);

        if (posting is null)
        {
            return Result.Failure<JobResponse>(JobErrors.PostingNotFound);
        }

        var employer = await _authorization.RequirePostingAccessAsync(
            posting, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure<JobResponse>(employer.Error);
        }

        var now = _clock.UtcNow;
        var filled = posting.FillPlace(now);

        if (filled.IsFailure)
        {
            return Result.Failure<JobResponse>(filled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(posting, employer.Value, now, canManage: true));
    }
}
