using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Promotions;

/// <summary>
/// Starts a promotion.
/// <para>
/// The featured flag is applied by the handler for the event this raises, so the flag and the
/// window can never disagree — which is the failure mode a boolean on the listing has and the
/// reason a promotion is its own aggregate.
/// </para>
/// </summary>
public sealed class CreatePromotionCommandHandler
    : ICommandHandler<CreatePromotionCommand, ListingPromotionResponse>
{
    private readonly IListingPromotionRepository _promotions;
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatePromotionCommandHandler(
        IListingPromotionRepository promotions,
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _promotions = promotions;
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ListingPromotionResponse>> Handle(
        CreatePromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ListingPromotionResponse>(caller.Error);
        }

        var kind = MarketplaceWireNames.ParsePromotionKind(command.Request.Kind);

        if (kind is null)
        {
            return Result.Failure<ListingPromotionResponse>(
                MarketplaceErrors.PromotionWindowInvalid);
        }

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingPromotionResponse>(MarketplaceErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;

        var promotion = ListingPromotion.Start(
            ListingPromotionId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.SellerId,
            kind.Value,
            command.Request.StartsAt ?? now,
            command.Request.EndsAt,
            now,
            caller.Value,
            command.Request.ExternalReference);

        if (promotion.IsFailure)
        {
            return Result.Failure<ListingPromotionResponse>(promotion.Error);
        }

        await _promotions.AddAsync(promotion.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ListingPromotionResponse
        {
            PromotionId = promotion.Value.Id.Value,
            ListingId = promotion.Value.ListingId.Value,
            Kind = promotion.Value.Kind.ToString(),
            StartsAt = promotion.Value.StartsAt,
            EndsAt = promotion.Value.EndsAt,
            IsActive = promotion.Value.IsActive(now),
            IsCancelled = promotion.Value.IsCancelled,
            ExternalReference = promotion.Value.ExternalReference,
            CreatedAt = promotion.Value.CreatedAt
        });
    }
}
