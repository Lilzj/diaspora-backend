namespace Diaspora.Modules.Marketplace;

/// <summary>
/// The feature gates the Marketplace module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c> — a gate below Limited is granted to phone-unverified accounts,
/// which is the B6 lesson made policy. Reading published listings needs no gate at all: someone
/// deciding whether this platform is worth joining should be able to see what is for sale on it.
/// </para>
/// <para>
/// <b><see cref="Browse"/> and <see cref="Sell"/> are not new names.</b> Both were declared
/// provisionally in the host's <c>ProvisionalCatalogContributor</c> from B2 onwards, against the
/// day this module landed, and the mobile client already renders gates against them. They move
/// here with their floors unchanged — Browse at Limited, <c>marketplace.listings.create</c> at
/// Verified — because a name the client switches on is a wire contract, and the provisional
/// entries are deleted in the same change. Leaving both declarations in place is not harmless:
/// the feature catalogue refuses two modules declaring one gate, and the resulting startup
/// failure takes down every request that resolves the catalogue, phone verification included.
/// </para>
/// <para>
/// <b>The band this module gates on is "looking is Limited, transacting is Verified".</b> It is
/// B9's rule restated for commerce, and it is sharper here: an offer is a financial commitment, a
/// reservation takes stock out of someone else's reach, and a listing takes a stranger's money.
/// Browsing and saving cost nobody anything.
/// </para>
/// </summary>
public static class MarketplaceFeatures
{
    /// <summary>Browse and search marketplace listings. The entry point to the whole module.</summary>
    public const string Browse = "marketplace.browse";

    /// <summary>
    /// List an item for sale. Verified only, because a listing takes a stranger's money.
    /// <para>
    /// The exact key the host declared provisionally from B2, at its original Verified floor.
    /// </para>
    /// </summary>
    public const string Sell = "marketplace.listings.create";

    /// <summary>Save listings and keep a watchlist.</summary>
    public const string Save = "marketplace.save";

    /// <summary>Make and answer offers. Verified only, because an offer is a commitment.</summary>
    public const string Offer = "marketplace.offer";

    /// <summary>Reserve an item. Verified only: a reservation holds stock off the market.</summary>
    public const string Reserve = "marketplace.reserve";

    /// <summary>Run a business or provider-backed storefront. Verified members only.</summary>
    public const string Storefront = "marketplace.storefront";

    /// <summary>Work the marketplace moderation queue. Staff only.</summary>
    public const string Moderate = "marketplace.moderate";
}
