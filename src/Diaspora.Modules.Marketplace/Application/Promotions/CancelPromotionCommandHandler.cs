using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Promotions;

/// <summary>
/// Cancels a promotion.
/// <para>
/// The featured flag comes down in the handler for the ending event, and only if no other live
/// promotion on the same listing still grants it.
/// </para>
/// </summary>
public sealed class CancelPromotionCommandHandler : ICommandHandler<CancelPromotionCommand>
{
    private readonly IListingPromotionRepository _promotions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public CancelPromotionCommandHandler(
        IListingPromotionRepository promotions,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _promotions = promotions;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        CancelPromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var promotion = await _promotions.GetAsync(
            ListingPromotionId.From(command.PromotionId), cancellationToken);

        if (promotion is null)
        {
            return Result.Failure(MarketplaceErrors.PromotionNotFound);
        }

        var cancelled = promotion.Cancel(caller.Value, _clock.UtcNow);

        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
