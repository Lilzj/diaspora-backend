using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Archives a listing, and refuses while anybody is still holding stock on it.
/// <para>
/// <b>The refusal is the interesting part.</b> A seller who archives a listing somebody has
/// reserved leaves a buyer with an arrangement to collect something that no longer exists on the
/// platform — so the command tells them to settle the reservations first. It is one of the very few
/// places this module refuses a seller an action on their own listing, and it is on a buyer's
/// behalf.
/// </para>
/// </summary>
public sealed class ArchiveListingCommandHandler : ICommandHandler<ArchiveListingCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public ArchiveListingCommandHandler(
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
        ArchiveListingCommand command, CancellationToken cancellationToken)
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

        var archived = listing.MoveTo(
            ListingStatus.Archived,
            _clock.UtcNow,
            _authorization.CallerId,
            command.Request?.Reason);

        if (archived.IsFailure)
        {
            return archived;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
