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
/// Publishes a posting.
/// <para>
/// The closing date is applied before publication rather than after, so a posting whose date is
/// already past is refused rather than published-then-expired — which would fire every matching
/// alert for a vacancy nobody can apply to.
/// </para>
/// </summary>
public sealed class PublishJobCommandHandler : ICommandHandler<PublishJobCommand, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public PublishJobCommandHandler(
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
        PublishJobCommand command, CancellationToken cancellationToken)
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

        // Before publication, deliberately. Publishing then expiring would fire every matching
        // alert for a vacancy nobody can apply to.
        if (command.Request.ClosingAt is { } closingAt)
        {
            var scheduled = posting.SetClosingDate(closingAt, now, baseRevision: null);

            if (scheduled.IsFailure)
            {
                return Result.Failure<JobResponse>(scheduled.Error);
            }
        }

        var published = posting.Publish(
            now, _authorization.CallerId, command.Request.OpenImmediately);

        if (published.IsFailure)
        {
            return Result.Failure<JobResponse>(published.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(
            posting, employer.Value, now, canManage: true));
    }
}
