using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Saved;

/// <summary>
/// Unsaves a job.
/// <para>
/// Unsaving what was never saved succeeds. The alternative — a 404 on a bookmark toggle — turns
/// a dropped connection and a double tap into an error the member has to think about.
/// </para>
/// </summary>
public sealed class UnsaveJobCommandHandler : ICommandHandler<UnsaveJobCommand>
{
    private readonly ISavedJobRepository _saved;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;

    public UnsaveJobCommandHandler(
        ISavedJobRepository saved,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization)
    {
        _saved = saved;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        UnsaveJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _authorization.RequireCandidateAsync(cancellationToken);

        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error);
        }

        var postingId = JobPostingId.From(command.JobId);

        var savedJob = await _saved.GetByCandidateAndPostingAsync(
            candidate.Value.Id, postingId, cancellationToken);

        if (savedJob is null)
        {
            return Result.Success();
        }

        _saved.Remove(savedJob);

        var posting = await _postings.GetAsync(postingId, cancellationToken);

        posting?.ApplySaveDelta(-1);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
