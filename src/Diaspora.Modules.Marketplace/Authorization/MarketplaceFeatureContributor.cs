using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Marketplace.Authorization;

/// <summary>
/// Declares the feature gates the Marketplace module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The split mirrors the permission band: browsing and saving are open at
/// <see cref="AccessTier.Limited"/>; listing, offering, reserving and running a storefront are
/// <see cref="AccessTier.Verified"/>; moderation is staff-only — a gate that told a member they
/// were merely locked out of the moderation queue would imply verifying unlocks it. It does not.
/// </para>
/// <para>
/// <b><c>marketplace.browse</c> and <c>marketplace.listings.create</c> keep the exact names and
/// floors the host declared provisionally from B2 onwards</b>, because the mobile client already
/// renders gates against them. The provisional entries are deleted in the same change: the feature
/// catalogue refuses two modules declaring one gate, and the resulting startup failure takes down
/// every request that resolves the catalogue — phone verification included, which is how every
/// integration test creates its member. B8 and B9 each record the same trap.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class MarketplaceFeatureContributor : IFeatureContributor
{
    public string Module => MarketplaceModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            MarketplaceFeatures.Browse, Module, "Browse and search marketplace listings.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            MarketplaceFeatures.Save, Module, "Save listings and keep a watchlist.")
        {
            MinimumTier = AccessTier.Limited
        };

        // The progression gate the client already knows about. A listing takes a stranger's money.
        yield return new FeatureDefinition(
            MarketplaceFeatures.Sell, Module, "List an item for sale.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            MarketplaceFeatures.Offer, Module, "Make and answer offers.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            MarketplaceFeatures.Reserve, Module, "Reserve an item for collection.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            MarketplaceFeatures.Storefront, Module, "Run a business or shop storefront.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            MarketplaceFeatures.Moderate, Module, "Work the marketplace moderation queue.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
