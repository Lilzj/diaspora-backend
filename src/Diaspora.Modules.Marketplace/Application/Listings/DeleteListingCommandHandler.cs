using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Soft-deletes a listing.
/// <para>
/// <b>The aggregate marks itself deleted rather than leaving it to the change tracker</b>, because
/// a delete that happens inside the tracker cannot also raise an event — and downstream
/// de-indexing needs one. That is B1.0.1's lesson and B6 records the silent failure it produces.
/// </para>
/// <para>
/// Refused while stock is held, for the same reason archiving is: a buyer with a collection
/// arranged should not find the listing simply gone.
/// </para>
/// </summary>
public sealed class DeleteListingCommandHandler : ICommandHandler<DeleteListingCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public DeleteListingCommandHandler(
        IMarketplaceListingRepository listings,
        IListingReservationRepository reservations,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _listings = listings;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        var seller = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (seller.IsFailure)
        {
            return Result.Failure(seller.Error);
        }

        var held = await _reservations.GetActiveForListingAsync(listing.Id, cancellationToken);

        if (held.Count > 0)
        {
            return Result.Failure(MarketplaceErrors.ListingReserved);
        }

        listing.MarkDeleted(_clock.UtcNow, _authorization.CallerId?.Value.ToString());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
