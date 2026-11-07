using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Infrastructure.Community;

/// <summary>
/// Makes listings commentable, reactable, reportable and bookmarkable, through B6.
/// <para>
/// <b>This one class is the whole of what B10 pays to get Community's social surface.</b>
/// <see cref="CommunityContentType.Marketplace"/> has existed in B6's own enum since it shipped, so
/// the type is claimed rather than invented. Registering it means questions under a listing,
/// reactions, community reports and bookmarks all work, and B10 wrote no social code at all.
/// </para>
/// <para>
/// <b>Community's reports and this module's are different things, deliberately.</b> A B6 report is
/// about content — an abusive question under a listing. A <c>ListingReport</c> is about a trade —
/// the item is counterfeit, the price is a bait. They are triaged by different people against
/// different evidence, and folding them into one queue would bury the fraud report under fifty rude
/// comments.
/// </para>
/// <para>
/// <b>Visibility is narrowed, never widened.</b> B6 takes the descriptor's audience as a ceiling, so
/// an unlisted or draft listing must not be described as public — that would let a comment on it
/// surface in a feed with no business showing it.
/// </para>
/// </summary>
public sealed class MarketplaceContentSource : ICommunityContentSource
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;

    public MarketplaceContentSource(
        IMarketplaceListingRepository listings, ISellerProfileRepository sellers)
    {
        _listings = listings;
        _sellers = sellers;
    }

    public CommunityContentType ContentType => CommunityContentType.Marketplace;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetForReadAsync(
            MarketplaceListingId.From(contentId), cancellationToken);

        return listing is null ? null : await DescribeAsync(listing, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        if (contentIds.Count == 0)
        {
            return described;
        }

        // One batched read for the listings and one for their sellers, rather than a pair per
        // identifier. Community batches its own calls, so this is bounded by a page.
        var listings = await _listings.GetManyAsync(
            [.. contentIds.Distinct().Select(MarketplaceListingId.From)], cancellationToken);

        var sellers = await _sellers.GetManyAsync(
            [.. listings.Select(listing => listing.SellerId).Distinct()], cancellationToken);

        foreach (var listing in listings)
        {
            described[listing.Id.Value] =
                Describe(listing, sellers.GetValueOrDefault(listing.SellerId));
        }

        return described;
    }

    // ApplyModerationAsync is deliberately not implemented (7 Nov 2026). Staff act on a listing only
    // through a Trust moderation case, carried out by MarketplaceListingModerationTarget with the real
    // moderator recorded; this hook recorded the listing's creator as the moderator.

    private async Task<ContentDescriptor> DescribeAsync(
        MarketplaceListing listing, CancellationToken cancellationToken)
    {
        var seller = await _sellers.GetForReadAsync(listing.SellerId, cancellationToken);

        return Describe(listing, seller);
    }

    private static ContentDescriptor Describe(
        MarketplaceListing listing, SellerProfile? seller) => new()
        {
            Reference = ContentReference.Create(
                CommunityContentType.Marketplace, listing.Id.Value),

            // The member behind the seller profile, because Community's model is about people. The
            // listing belongs to the profile; the author of the content is whoever owns it.
            AuthorId = seller?.OwnerUserId.Value ?? listing.CreatedByUserId.Value,
            Title = listing.Title.Value,
            Exists = !listing.IsDeleted,
            Visibility = ToCommunityVisibility(listing),
            CountryCode = listing.Location.CountryCode,
            AreaId = listing.Location.AreaId,

            // A sold or withdrawn listing stops taking questions: an answer nobody will act on is
            // worse than no comment box. A live one takes them, because "is it still available"
            // is the single most common thing a buyer wants to ask.
            AcceptsComments = ListingStatusTransitions.IsPubliclyVisible(listing.Status)
                && listing.Status != ListingStatus.Sold
                && listing.ModerationState is ListingModerationState.Clear
                    or ListingModerationState.Flagged
        };

    /// <summary>
    /// Projects B10's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// B6's vocabulary has no <c>Unlisted</c>, so a link-shared listing maps to its members-only
    /// band rather than to <c>Public</c>. Erring outward here would surface an unpublished listing's
    /// questions in a public feed, which is a privacy failure rather than a rendering bug — and a
    /// draft is private regardless of what its visibility field says.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(MarketplaceListing listing)
    {
        if (!ListingStatusTransitions.IsPubliclyVisible(listing.Status))
        {
            return VisibilityLevel.Private;
        }

        return listing.Visibility switch
        {
            ListingVisibility.Public => VisibilityLevel.Public,
            ListingVisibility.Unlisted => VisibilityLevel.Members,
            _ => VisibilityLevel.Private
        };
    }
}
