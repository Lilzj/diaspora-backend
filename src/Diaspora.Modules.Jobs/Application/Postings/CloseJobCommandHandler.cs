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
/// Closes a posting.
/// <para>
/// <c>filled</c> and <c>withdrawn</c> land on different statuses because they mean different
/// things to somebody with a live application: one says the seat is gone, the other says the
/// employer changed their mind. Both are worth telling an applicant, and neither is "you were
/// rejected".
/// </para>
/// </summary>
public sealed class CloseJobCommandHandler : ICommandHandler<CloseJobCommand, JobResponse>
{
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public CloseJobCommandHandler(
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
        CloseJobCommand command, CancellationToken cancellationToken)
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

        var target = string.Equals(
            command.Request.Outcome, "filled", StringComparison.OrdinalIgnoreCase)
            ? JobStatus.Filled
            : JobStatus.Expired;

        var closed = posting.MoveTo(
            target, now, _authorization.CallerId, command.Request.Reason);

        if (closed.IsFailure)
        {
            return Result.Failure<JobResponse>(closed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(JobMapper.ToResponse(
            posting, employer.Value, now, canManage: true));
    }
}
