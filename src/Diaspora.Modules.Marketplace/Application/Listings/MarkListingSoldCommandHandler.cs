using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Marks units sold directly.
/// <para>
/// The aggregate refuses to sell more than is available, so a seller who records a sale of four
/// against three remaining is told rather than allowed to invent stock. Everything downstream — the
/// sold count, the seller's statistics, the analytics, the closing of open negotiations — happens in
/// the handler for the sold event the aggregate raises, which is the same path a reservation
/// completion takes.
/// </para>
/// </summary>
public sealed class MarkListingSoldCommandHandler : ICommandHandler<MarkListingSoldCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public MarkListingSoldCommandHandler(
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
        MarkListingSoldCommand command, CancellationToken cancellationToken)
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

        var request = command.Request;

        var sold = listing.MarkSold(
            request?.Quantity,
            _clock.UtcNow,
            request?.BuyerUserId is { } buyerId && buyerId != Guid.Empty
                ? UserId.From(buyerId)
                : null,
            request?.Amount,
            request?.Currency);

        if (sold.IsFailure)
        {
            return sold;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
