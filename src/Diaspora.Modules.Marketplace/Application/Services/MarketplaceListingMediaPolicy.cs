using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Answers Media's question about a photograph attached to a marketplace listing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Every media asset is created <c>Private</c> — the right default, and
/// the reason immigration documents are not indexed. But a listing photograph is not private: the
/// listing cannot even be published without one, and the whole point of publishing is that
/// strangers look at it. With nothing to say otherwise, Media refused every listing photograph to
/// everyone except the member who uploaded it, and every buyer saw a "no photograph" panel on a
/// listing that provably had one.
/// </para>
/// <para>
/// <b>The rule is the listing's own.</b> This does not restate when a listing is visible; it asks
/// <see cref="MarketplaceAuthorizationService.CanSeeAsync"/>, the same method the detail screen
/// uses. So a photograph is readable exactly when the listing carrying it is — drafts stay
/// invisible, a hidden listing's photographs go with it, an unlisted one is reachable by whoever
/// has the link, and a listing pulled by moderation takes its images down in the same instant. A
/// second copy of that rule here would drift, and the drift would be a leak.
/// </para>
/// <para>
/// <b>It never denies.</b> A denial in this seam is final and would remove access Media had
/// already granted — including the seller's access to their own photograph, which is the one thing
/// they need after deleting a listing they mean to restore. Everything this does not positively
/// grant is an abstention, which leaves Media's own answer standing. The effect is that this
/// policy can only widen, never take away.
/// </para>
/// </remarks>
public sealed class MarketplaceListingMediaPolicy : IMediaAccessPolicy
{
    /// <summary>
    /// The roles a published listing makes readable.
    /// </summary>
    /// <remarks>
    /// A closed list, not "anything attached to a visible listing". Sellers attach documents to
    /// listings — proof of ownership, a service history, an import receipt — under the
    /// <c>document</c> role, and those are evidence for the platform rather than part of the
    /// advertisement. Granting by listing visibility alone would publish them.
    /// </remarks>
    private static readonly string[] PubliclyVisibleRoles =
    [
        MediaReferenceRoles.Cover,
        MediaReferenceRoles.Gallery,
        MediaReferenceRoles.Banner,
        MediaReferenceRoles.Inline
    ];

    private readonly IMarketplaceListingRepository _listings;
    private readonly MarketplaceAuthorizationService _authorization;

    public MarketplaceListingMediaPolicy(
        IMarketplaceListingRepository listings,
        MarketplaceAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(authorization);

        _listings = listings;
        _authorization = authorization;
    }

    public MediaOwnerType OwnerType => MediaOwnerType.MarketplaceListing;

    public async Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestedLevel is not MediaAccessLevel.Read)
        {
            // Editing and deleting a file stay with whoever uploaded it. Seeing a listing is not
            // a reason to be able to change its pictures.
            return MediaAccessDecision.Abstain();
        }

        if (!PubliclyVisibleRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            return MediaAccessDecision.Abstain();
        }

        // Untracked: this runs on the read path of every image in a gallery, and a tracked graph
        // per photograph would put the change tracker between a member and a picture.
        var listing = await _listings.GetForReadAsync(
            MarketplaceListingId.From(request.OwnerId), cancellationToken);

        if (listing is null)
        {
            return MediaAccessDecision.Abstain();
        }

        return await _authorization.CanSeeAsync(listing, cancellationToken)
            ? MediaAccessDecision.Allow("The listing carrying this image is visible to the caller.")
            : MediaAccessDecision.Abstain();
    }
}
