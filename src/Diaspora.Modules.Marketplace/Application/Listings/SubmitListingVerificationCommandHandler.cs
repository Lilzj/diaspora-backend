using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Infrastructure.Trust;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Sends a listing-level verification claim to Trust and records that it went.
/// <para>
/// <b>Trust is asked first and the listing is stamped second, deliberately.</b> The case identifier
/// Trust returns is what lets an outcome find this listing again — Trust's events name a case, and
/// a claim recorded without one is a claim whose approval arrives with nowhere to land.
/// </para>
/// </summary>
public sealed class SubmitListingVerificationCommandHandler
    : ICommandHandler<SubmitListingVerificationCommand>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly MarketplaceTrustVerificationProvider _trust;
    private readonly IClock _clock;

    public SubmitListingVerificationCommandHandler(
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        MarketplaceTrustVerificationProvider trust,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _trust = trust;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SubmitListingVerificationCommand command, CancellationToken cancellationToken)
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

        var caller = _authorization.CallerId!;

        var submitted = await _trust.SubmitListingAsync(
            listing,
            caller,
            command.Request?.EvidenceMediaIds ?? [],
            command.Request?.Notes,
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure(submitted.Error);
        }

        var recorded = listing.SubmitForVerification(
            _clock.UtcNow, caller, submitted.Value);

        if (recorded.IsFailure)
        {
            return recorded;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
