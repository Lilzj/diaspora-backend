using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>Detaches media. Idempotent — detaching what is not there succeeds.</summary>
public sealed class RemoveListingMediaCommandHandler : ICommandHandler<RemoveListingMediaCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;

    public RemoveListingMediaCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        RemoveListingMediaCommand command, CancellationToken cancellationToken)
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

        var role = MarketplaceWireNames.ParseMediaRole(command.Role);

        if (role is { } named)
        {
            var detached = listing.DetachMedia(command.MediaId, named);

            if (detached.IsFailure)
            {
                return detached;
            }
        }
        else
        {
            // No role named: detach every role the asset holds. A client removing a photograph
            // from the gallery should not have to know whether it was also the cover.
            var roles = listing.MediaReferences
                .Where(reference => reference.MediaId == command.MediaId)
                .Select(reference => reference.Role)
                .ToList();

            foreach (var held in roles)
            {
                var detached = listing.DetachMedia(command.MediaId, held);

                if (detached.IsFailure)
                {
                    return detached;
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
