using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Saved;

/// <summary>Saves a job.</summary>
public sealed class SaveJobCommandHandler : ICommandHandler<SaveJobCommand>
{
    private readonly ISavedJobRepository _saved;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveJobCommandHandler(
        ISavedJobRepository saved,
        IJobPostingRepository postings,
        IUnitOfWork unitOfWork,
        JobAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _saved = saved;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SaveJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _authorization.RequireCandidateAsync(cancellationToken);

        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error);
        }

        var postingId = JobPostingId.From(command.JobId);
        var posting = await _postings.GetAsync(postingId, cancellationToken);

        if (posting is null || !await _authorization.CanSeeAsync(posting, cancellationToken))
        {
            return Result.Failure(JobErrors.PostingNotFound);
        }

        var existing = await _saved.GetByCandidateAndPostingAsync(
            candidate.Value.Id, postingId, cancellationToken);

        // Idempotent: a repeated tap updates the note rather than failing.
        if (existing is not null)
        {
            var refreshed = existing.Update(
                command.Request.Note, command.Request.RemindBeforeClosing);

            if (refreshed.IsFailure)
            {
                return refreshed;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        var count = await _saved.CountForCandidateAsync(candidate.Value.Id, cancellationToken);

        if (count >= JobLimits.MaxSavedJobs)
        {
            return Result.Failure(JobErrors.SavedJobLimit);
        }

        var savedJob = SavedJob.Create(
            SavedJobId.From(_guids.NewSequentialGuid()),
            candidate.Value.Id,
            candidate.Value.UserId,
            postingId,
            command.Request.Note,
            _clock.UtcNow,
            command.Request.RemindBeforeClosing);

        if (savedJob.IsFailure)
        {
            return Result.Failure(savedJob.Error);
        }

        await _saved.AddAsync(savedJob.Value, cancellationToken);

        // The card's counter. Not a revision bump — saving is not an edit to the posting.
        posting.ApplySaveDelta(1);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
