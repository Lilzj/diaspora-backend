using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Infrastructure.Trust;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>
/// Sends a seller verification claim to Trust and records that it went.
/// <para>
/// Trust is asked first and the profile is stamped second: the case identifier it returns is what
/// lets the outcome find this seller again, and a claim recorded without one is a claim whose
/// approval arrives with nowhere to land.
/// </para>
/// </summary>
public sealed class SubmitSellerVerificationCommandHandler
    : ICommandHandler<SubmitSellerVerificationCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly MarketplaceTrustVerificationProvider _trust;
    private readonly IClock _clock;

    public SubmitSellerVerificationCommandHandler(
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        MarketplaceTrustVerificationProvider trust,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _trust = trust;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SubmitSellerVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var seller = await _authorization.RequireSellerAsync(cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure(seller.Error);
        }

        var caller = _authorization.CallerId!;

        var submitted = await _trust.SubmitSellerAsync(
            seller.Value,
            caller,
            command.Request?.EvidenceMediaIds ?? [],
            command.Request?.Notes,
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure(submitted.Error);
        }

        var recorded = seller.Value.SubmitForVerification(
            _clock.UtcNow, caller, submitted.Value);

        if (recorded.IsFailure)
        {
            return recorded;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
