using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Infrastructure.Trust;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Lodges a verification claim for one property with Trust.
/// <para>
/// Trust is asked before the aggregate is marked pending, and the order matters: marking first
/// would leave a property stuck in Pending whenever Trust was unavailable, and a pending mark is
/// exactly what refuses a second submission — so the landlord could never retry.
/// </para>
/// </summary>
public sealed class SubmitListingVerificationCommandHandler
    : ICommandHandler<SubmitListingVerificationCommand, Guid>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingTrustVerificationProvider _trust;
    private readonly IClock _clock;

    public SubmitListingVerificationCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingTrustVerificationProvider trust,
        IClock clock)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _trust = trust;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(
        SubmitListingVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<Guid>(listing.Error);
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        var submitted = await _trust.SubmitListingAsync(
            listing.Value,
            caller.Value,
            command.Request.EvidenceMediaIds ?? [],
            command.Request.Notes,
            cancellationToken);

        if (submitted.IsFailure)
        {
            return submitted;
        }

        var marked = listing.Value.SubmitForVerification(
            submitted.Value, caller.Value, _clock.UtcNow);

        if (marked.IsFailure)
        {
            return Result.Failure<Guid>(marked.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(submitted.Value);
    }
}
