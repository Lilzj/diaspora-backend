using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Opens a negotiation.
/// <para>
/// <b>Five checks before the aggregate sees anything, and each refuses a different bad outcome.</b>
/// The listing must be taking offers at all; the currency must match, because nothing here
/// converts; the amount must clear the seller's private floor, so an insulting offer never reaches
/// them; the buyer must not already have one open, so a listing does not accumulate five
/// negotiations with one person; and the daily bound stops a script lowballing the whole
/// marketplace.
/// </para>
/// <para>
/// <b>The floor is checked and never disclosed.</b> The refusal says the offer is below what the
/// seller will consider, and does not say by how much — a message that leaked the number would make
/// the floor a public asking price.
/// </para>
/// </summary>
public sealed class MakeOfferCommandHandler : ICommandHandler<MakeOfferCommand, OfferResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingOfferRepository _offers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public MakeOfferCommandHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingOfferRepository offers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _sellers = sellers;
        _offers = offers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OfferResponse>> Handle(
        MakeOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OfferResponse>(caller.Error);
        }

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;

        if (!listing.AcceptsOffers(now))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OffersNotAccepted);
        }

        var seller = await _sellers.GetAsync(listing.SellerId, cancellationToken);

        if (seller is null || !seller.CanTrade)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.SellerSuspended);
        }

        if (seller.OwnerUserId == caller.Value)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferSelfBid);
        }

        var existing = await _offers.GetOpenForBuyerAsync(
            listing.Id, caller.Value, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferAlreadyOpen);
        }

        var madeToday = await _offers.CountByBuyerSinceAsync(
            caller.Value, now.AddDays(-1), cancellationToken);

        if (madeToday >= _options.Value.MaxOffersPerBuyerPerDay)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.TooManyOffers);
        }

        // The listing's currency, not the client's. An offer in another unit is refused rather than
        // silently compared, because a seller reading "1,200" against "1,500" cannot tell.
        var currency = command.Request.Currency ?? listing.Price.CurrencyCode;

        if (!listing.Price.SharesCurrencyWith(currency))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.CurrencyMismatch);
        }

        var quantity = command.Request.Quantity ?? 1;

        if (quantity > listing.Inventory.Available && listing.Inventory.IsTracked)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferQuantityInvalid);
        }

        var terms = OfferTerms.Create(
            command.Request.Amount, currency, quantity, command.Request.Message);

        if (terms.IsFailure)
        {
            return Result.Failure<OfferResponse>(terms.Error);
        }

        // Checked, never disclosed. See the class summary.
        if (!listing.Price.Accepts(terms.Value.Amount))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferBelowMinimum);
        }

        var expiresAt = command.Request.ExpiresAt
            ?? now.AddHours(Math.Max(1, _options.Value.DefaultOfferHours));

        var offer = ListingOffer.Open(
            ListingOfferId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.SellerId,
            caller.Value,
            seller.OwnerUserId,
            terms.Value,
            expiresAt,
            now,
            _guids.NewSequentialGuid());

        if (offer.IsFailure)
        {
            return Result.Failure<OfferResponse>(offer.Error);
        }

        await _offers.AddAsync(offer.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            OfferMapper.ToResponse(offer.Value, caller.Value, listing, seller));
    }
}
