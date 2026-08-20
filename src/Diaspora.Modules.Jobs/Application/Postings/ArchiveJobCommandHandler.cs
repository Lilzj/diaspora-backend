using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>Archives a posting.</summary>
public sealed class ArchiveJobCommandHandler : ICommandHandler<ArchiveJobCommand>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public ArchiveJobCommandHandler(
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

    public async Task<Result> Handle(
        ArchiveJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var posting = await _postings.GetAsync(
            JobPostingId.From(command.JobId), cancellationToken);

        if (posting is null)
        {
            return Result.Failure(JobErrors.PostingNotFound);
        }

        var employer = await _authorization.RequirePostingAccessAsync(
            posting, EmployerMemberRole.Recruiter, cancellationToken);

        if (employer.IsFailure)
        {
            return Result.Failure(employer.Error);
        }

        var archived = posting.MoveTo(
            JobStatus.Archived, _clock.UtcNow, _authorization.CallerId, reason: null);

        if (archived.IsFailure)
        {
            return archived;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
