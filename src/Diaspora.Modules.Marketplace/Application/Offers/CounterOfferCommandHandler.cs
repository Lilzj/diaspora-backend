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
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Applies a counter from whichever party's turn it is.
/// <para>
/// The turn rule, the round ceiling, the currency match and the expiry are all the aggregate's, so
/// this handler is thin by design: it resolves the parties, builds the terms and lets the
/// negotiation decide whether the move is legal.
/// </para>
/// </summary>
public sealed class CounterOfferCommandHandler : ICommandHandler<CounterOfferCommand, OfferResponse>
{
    private readonly IListingOfferRepository _offers;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CounterOfferCommandHandler(
        IListingOfferRepository offers,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _offers = offers;
        _listings = listings;
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<OfferResponse>> Handle(
        CounterOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OfferResponse>(caller.Error);
        }

        var offer = await _offers.GetAsync(
            ListingOfferId.From(command.OfferId), cancellationToken);

        if (offer is null || !offer.Involves(caller.Value))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferNotFound);
        }

        var now = _clock.UtcNow;

        var terms = OfferTerms.Create(
            command.Request.Amount,
            offer.Terms.CurrencyCode,
            command.Request.Quantity ?? offer.Terms.Quantity,
            command.Request.Message);

        if (terms.IsFailure)
        {
            return Result.Failure<OfferResponse>(terms.Error);
        }

        var expiresAt = command.Request.ExpiresAt
            ?? now.AddHours(Math.Max(1, _options.Value.DefaultOfferHours));

        var countered = offer.Counter(
            caller.Value, terms.Value, expiresAt, now, _guids.NewSequentialGuid());

        if (countered.IsFailure)
        {
            return Result.Failure<OfferResponse>(countered.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var listing = await _listings.GetForReadAsync(offer.ListingId, cancellationToken);
        var seller = await _sellers.GetForReadAsync(offer.SellerId, cancellationToken);

        return Result.Success(
            OfferMapper.ToResponse(offer, caller.Value, listing, seller));
    }
}
