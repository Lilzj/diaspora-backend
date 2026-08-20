using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Attaches media by reference.
/// <para>
/// The role defaults to the cover when the listing has no photograph yet and to the gallery
/// afterwards, which is what a seller means by uploading one first. A second cover replaces the
/// first rather than being refused — picking a new main photograph is the obvious thing to do, and
/// an error telling them to remove the old one would be the module being pedantic about its own
/// data model.
/// </para>
/// </summary>
public sealed class AttachListingMediaCommandHandler : ICommandHandler<AttachListingMediaCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;

    public AttachListingMediaCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        AttachListingMediaCommand command, CancellationToken cancellationToken)
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

        var role = MarketplaceWireNames.ParseMediaRole(command.Request.Role)
            ?? ListingSearchProjectionService.DefaultRoleFor(listing.CoverImage is null);

        var attached = listing.AttachMedia(
            command.Request.MediaId, role, command.Request.Caption);

        if (attached.IsFailure)
        {
            return attached;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
