using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>Applies a tag replacement.</summary>
public sealed class SetListingTagsCommandHandler : ICommandHandler<SetListingTagsCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;

    public SetListingTagsCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        SetListingTagsCommand command, CancellationToken cancellationToken)
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

        var applied = listing.SetTags(
            [.. command.Request.Tags], command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return applied;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
