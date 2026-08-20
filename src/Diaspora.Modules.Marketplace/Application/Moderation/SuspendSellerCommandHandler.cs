using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Suspends or reinstates a seller.
/// <para>
/// <b>Nothing here touches their listings.</b> The cascade happens in the handler for the status
/// event, bounded and after this write commits — because a seller with four hundred listings would
/// otherwise make suspending a shop a minute-long transaction, and a transaction that long is one
/// that times out at exactly the moment a moderator most needs it to work.
/// </para>
/// </summary>
public sealed class SuspendSellerCommandHandler : ICommandHandler<SuspendSellerCommand>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public SuspendSellerCommandHandler(
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SuspendSellerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure(MarketplaceErrors.NotModerator);
        }

        var seller = await _sellers.GetAsync(
            SellerProfileId.From(command.SellerId), cancellationToken);

        if (seller is null)
        {
            return Result.Failure(MarketplaceErrors.SellerNotFound);
        }

        var now = _clock.UtcNow;

        var applied = command.Request.Action.Trim().ToLowerInvariant() switch
        {
            "suspend" => seller.Suspend(command.Request.Reason, caller.Value, now),
            "reinstate" => seller.Reinstate(caller.Value, now),
            _ => Result.Failure(MarketplaceErrors.ModerationNoteInvalid)
        };

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
