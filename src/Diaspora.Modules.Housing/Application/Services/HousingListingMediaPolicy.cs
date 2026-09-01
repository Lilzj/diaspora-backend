using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Answers Media's question about a photograph attached to a housing listing.
/// </summary>
/// <remarks>
/// <para>
/// The twin of <c>MarketplaceListingMediaPolicy</c>, and it exists for the same reason: a media
/// asset is created <c>Private</c>, so Media refused every property photograph to everyone except
/// the landlord who uploaded it. Prospective tenants saw <em>"no photos of this property"</em> on
/// listings that could not have been published without one.
/// </para>
/// <para>
/// <b>The rule is the listing's own.</b> It asks
/// <see cref="HousingAuthorizationService.CanSeeAsync"/> — the same method the detail screen uses
/// — rather than restating when a property is visible. Drafts and archived listings stay dark,
/// an unlisted one is reachable by whoever holds the link, and a listing withdrawn by moderation
/// takes its photographs with it in the same instant.
/// </para>
/// <para>
/// <b>It never denies</b>, only grants or abstains, so it can widen access but never remove
/// access Media had already allowed — including the landlord's own view of their photograph after
/// they archive a listing.
/// </para>
/// </remarks>
public sealed class HousingListingMediaPolicy : IMediaAccessPolicy
{
    /// <summary>
    /// The roles a visible listing makes readable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>floor-plan</c> is here and <c>document</c> is not, which is the distinction worth
    /// stating. A floor plan is part of the advertisement — it is why a tenant clicks through.
    /// A document attached to a listing is a tenancy agreement, an EPC certificate or proof of
    /// ownership, and none of those become public because the property did.
    /// </para>
    /// </remarks>
    private static readonly string[] PubliclyVisibleRoles =
    [
        MediaReferenceRoles.Cover,
        MediaReferenceRoles.Gallery,
        MediaReferenceRoles.Banner,
        MediaReferenceRoles.FloorPlan,
        MediaReferenceRoles.Inline
    ];

    private readonly IHousingListingRepository _listings;
    private readonly HousingAuthorizationService _authorization;

    public HousingListingMediaPolicy(
        IHousingListingRepository listings,
        HousingAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(authorization);

        _listings = listings;
        _authorization = authorization;
    }

    public MediaOwnerType OwnerType => MediaOwnerType.HousingListing;

    public async Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestedLevel is not MediaAccessLevel.Read)
        {
            // Seeing a property is not a reason to be able to change its pictures.
            return MediaAccessDecision.Abstain();
        }

        if (!PubliclyVisibleRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            return MediaAccessDecision.Abstain();
        }

        // Untracked: this runs once per photograph on a gallery, and a tracked graph per image
        // would put the change tracker between a tenant and a picture.
        var listing = await _listings.GetForReadAsync(
            HousingListingId.From(request.OwnerId), cancellationToken);

        if (listing is null)
        {
            return MediaAccessDecision.Abstain();
        }

        return await _authorization.CanSeeAsync(listing, cancellationToken)
            ? MediaAccessDecision.Allow("The listing carrying this image is visible to the caller.")
            : MediaAccessDecision.Abstain();
    }
}
