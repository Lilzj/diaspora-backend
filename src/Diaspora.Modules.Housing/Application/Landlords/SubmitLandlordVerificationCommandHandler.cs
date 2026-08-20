using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Infrastructure.Trust;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>
/// Lodges a verification claim for the caller's landlord profile with Trust.
/// <para>
/// <b>Trust is asked before the aggregate is marked pending, and the order matters.</b> Marking
/// first would leave a profile permanently stuck in Pending whenever Trust was unavailable — and a
/// pending mark is what refuses a second submission, so the member could never retry.
/// </para>
/// </summary>
public sealed class SubmitLandlordVerificationCommandHandler
    : ICommandHandler<SubmitLandlordVerificationCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingTrustVerificationProvider _trust;
    private readonly Platform.Services.Abstractions.IClock _clock;

    public SubmitLandlordVerificationCommandHandler(
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingTrustVerificationProvider trust,
        Platform.Services.Abstractions.IClock clock)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _trust = trust;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(
        SubmitLandlordVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var landlord = await _authorization.RequireLandlordAsync(cancellationToken);

        if (landlord.IsFailure)
        {
            return Result.Failure<Guid>(landlord.Error);
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        // Ask Trust first. See the class summary for why the order is not arbitrary.
        var submitted = await _trust.SubmitLandlordAsync(
            landlord.Value,
            caller.Value,
            command.Request.EvidenceMediaIds ?? [],
            command.Request.Notes,
            cancellationToken);

        if (submitted.IsFailure)
        {
            return submitted;
        }

        var marked = landlord.Value.SubmitForVerification(
            submitted.Value, caller.Value, _clock.UtcNow);

        if (marked.IsFailure)
        {
            return Result.Failure<Guid>(marked.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(submitted.Value);
    }
}
