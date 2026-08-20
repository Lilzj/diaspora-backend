using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Applies a Trust outcome to whichever seller or listing raised the claim.
/// <para>
/// <b>A service rather than a method on one of the handlers, and the reason is the container.</b>
/// Four Trust contracts land on two handler classes, and both need this. A handler that depended on
/// another handler would not resolve at all: MediatR registers a handler as
/// <c>INotificationHandler&lt;T&gt;</c> and not as its own concrete type, so the injection fails
/// during provider validation — at startup, taking every other module down with it. Extracting the
/// shared path is the fix, and it is the better shape anyway: handlers should not know about each
/// other.
/// </para>
/// <para>
/// <b>Correlation is by verification identifier, not by member.</b> A member may have a seller
/// profile and a hundred listings, and Trust's events name a case rather than a subject the
/// marketplace would recognise — so the seller and the listing were each stamped with the case
/// identifier at submission, and this is the read that finds them again. An outcome for a case
/// nobody in this module raised is ignored silently, because Trust serves every vertical and most of
/// its decisions are none of B10's business.
/// </para>
/// <para>
/// <b>Every write is guarded on the decision timestamp inside the aggregate.</b> Delivery is
/// at-least-once and unordered, so a delayed redelivery of an old approval must not re-verify a
/// seller Trust has since revoked.
/// </para>
/// </summary>
public sealed class TrustOutcomeApplier
{
    private readonly ISellerProfileRepository _sellers;
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrustOutcomeApplier> _logger;

    public TrustOutcomeApplier(
        ISellerProfileRepository sellers,
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        ILogger<TrustOutcomeApplier> logger)
    {
        _sellers = sellers;
        _listings = listings;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Finds the seller or the listing the case belongs to and applies the decision to it.
    /// <para>
    /// Sellers are checked first because a seller-level claim is the common one and a listing-level
    /// claim is the exception. Neither matching is an ordinary outcome rather than an error.
    /// </para>
    /// </summary>
    public async Task ApplyAsync(
        Guid verificationId,
        ListingVerificationState state,
        string? badge,
        string? reason,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken = default)
    {
        if (verificationId == Guid.Empty)
        {
            return;
        }

        var seller = await _sellers.GetByVerificationIdAsync(verificationId, cancellationToken);

        if (seller is not null)
        {
            seller.ApplyVerificationDecision(state, verificationId, badge, reason, decidedAt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Trust decision {State} applied to marketplace seller {SellerId}.",
                state,
                seller.Id.Value);

            return;
        }

        var listing = await _listings.GetByVerificationIdAsync(verificationId, cancellationToken);

        if (listing is null)
        {
            return;
        }

        listing.ApplyVerificationDecision(state, verificationId, badge, reason, decidedAt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Trust decision {State} applied to marketplace listing {ListingId}.",
            state,
            listing.Id.Value);
    }
}
