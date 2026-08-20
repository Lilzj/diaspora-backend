using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Withdraws an application.
/// <para>
/// The posting's applicant count is decremented so the card stays honest — an employer looking at
/// "40 applicants" when eight have withdrawn is reading the wrong number to decide whether the
/// advert is working.
/// </para>
/// </summary>
public sealed class WithdrawApplicationCommandHandler
    : ICommandHandler<WithdrawApplicationCommand>
{
    private readonly IJobApplicationRepository _applications;
    private readonly IJobPostingRepository _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobAuthorizationService _authorization;
    private readonly IClock _clock;

    public WithdrawApplicationCommandHandler(
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

    public async Task<Result> Handle(
        WithdrawApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await _applications.GetAsync(
            JobApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        // The applicant themselves, and nobody else — not even staff. Withdrawing on somebody's
        // behalf is not a support action this module offers.
        var owner = _authorization.RequireOwner(application.ApplicantUserId);

        if (owner.IsFailure)
        {
            return Result.Failure(owner.Error);
        }

        var withdrawn = application.Withdraw(
            _clock.UtcNow, owner.Value, command.Request.Reason);

        if (withdrawn.IsFailure)
        {
            return withdrawn;
        }

        var posting = await _postings.GetAsync(application.PostingId, cancellationToken);

        posting?.ApplyApplicationDelta(-1);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
