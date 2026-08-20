using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>Changes a candidate's profile visibility.</summary>
public sealed class ChangeCandidateVisibilityCommandHandler
    : ICommandHandler<ChangeCandidateVisibilityCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;

    public ChangeCandidateVisibilityCommandHandler(
        IUnitOfWork unitOfWork, JobAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        ChangeCandidateVisibilityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _authorization.RequireCandidateAsync(cancellationToken);

        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error);
        }

        var visibility = JobWireNames.ParseCandidateVisibility(command.Visibility);

        if (visibility is null)
        {
            return Result.Failure(JobErrors.HeadlineInvalid);
        }

        var changed = candidate.Value.ChangeVisibility(visibility.Value);

        if (changed.IsFailure)
        {
            return changed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
