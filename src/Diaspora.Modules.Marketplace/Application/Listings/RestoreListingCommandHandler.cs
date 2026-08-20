using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Restores a deleted or archived listing.
/// <para>
/// <b>It reads through the soft-delete filter, and that is the whole trick.</b> The global query
/// filter hides exactly the row an undelete has to load, so a restore written against the ordinary
/// read finds nothing and reports "not found" for a listing the seller can see in their own trash.
/// The repository publishes one deliberately named method for it.
/// </para>
/// </summary>
public sealed class RestoreListingCommandHandler : ICommandHandler<RestoreListingCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public RestoreListingCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        RestoreListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The one read in the module that ignores the soft-delete filter. See the class summary.
        var listing = await _listings.GetIncludingDeletedAsync(
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

        if (listing.IsDeleted)
        {
            listing.Restore();
        }
        else if (listing.Status == ListingStatus.Archived)
        {
            var moved = listing.MoveTo(
                ListingStatus.Draft, _clock.UtcNow, _authorization.CallerId, reason: null);

            if (moved.IsFailure)
            {
                return moved;
            }
        }
        else
        {
            return Result.Failure(MarketplaceErrors.ListingNotArchived);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
