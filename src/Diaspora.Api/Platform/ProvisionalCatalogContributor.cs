using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Api.Platform;

/// <summary>
/// A holding pen for permissions and feature gates whose owning module has not been built
/// yet.
/// <para>
/// B2 declared these inside Identity, which is the thing B2.6 set out to stop: Identity
/// had no business knowing what a marketplace listing is, and every future module would
/// have had to edit Identity's source on the way in. They are not deleted, because the
/// mobile client already renders gates against these names and the access-state response
/// would silently shrink; they are moved to the composition root, where they are visibly
/// nobody's.
/// </para>
/// <para>
/// <strong>Each entry names the module that will claim it.</strong> When that module lands,
/// it declares its own permissions and gates through the ordinary contributor interfaces
/// and the corresponding lines here are deleted — which is a one-line diff in a file that
/// exists to shrink. When the file is empty, delete it.
/// </para>
/// <para>
/// This class also happens to prove a point the Bookmarks module makes at greater length:
/// contributions are found wherever they live. Nothing about this file is special to the
/// host project.
/// </para>
/// </summary>
public sealed class ProvisionalCatalogContributor : IPermissionContributor, IFeatureContributor
{
    /// <summary>
    /// Deliberately not the name of a real module. Anything attributed to "Platform
    /// (provisional)" in the manifest is a debt entry, and it should read like one.
    /// </summary>
    public string Module => "Platform (provisional)";

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        // TODO(undecided): move to whichever module enforces publishing — and PSR-1 could not
        // decide which that is.
        //
        // Both candidate modules now exist and **neither enforces this key**. Nothing in the
        // platform reads it: no endpoint requires it, no handler checks it, and no policy names
        // it. Guides gates publication with guides.publish and Community with
        // community.posting — each module already owns its own answer, declared under its own
        // prefix.
        //
        // So this is not a placeholder waiting for a module. It is a key B2 minted, that the
        // frontend's capability map still holds, and that the platform has never once consulted.
        // Moving it to a module would invent an owner for something that module does not enforce,
        // which is worse than leaving it visibly nobody's. Retiring it is a versioned
        // authorisation change, because a key in a shipped access token cannot simply stop being
        // issued. Recorded as DEBT-045.
        yield return new PermissionDefinition(
            "content:publish", Module, "Publish community content.")
        {
            MinimumTier = AccessTier.Verified
        };

        // TODO(Messaging): move to the Messaging module. Recorded as DEBT-045.
        yield return new PermissionDefinition(
            "messaging:send", Module, "Send direct messages.")
        {
            MinimumTier = AccessTier.Verified
        };

        // jobs:apply moved to the Jobs module in B9, which now declares it from
        // JobPermissionContributor at the same Verified floor. The key is unchanged, because
        // the mobile client already switches on it.

        // organizations:create moved to the Community Life module in B12, which now declares it
        // from CommunityLifePermissionContributor at the same Verified floor. The key is
        // unchanged, because the mobile client already switches on it.
    }

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        // ─── Browsable from limited access ──────────────────────────────
        // community.browse moved to the Community module in PSR-1, which now declares it from
        // CommunityFeatureContributor at the same Limited floor. The name is unchanged, because
        // the mobile client already switches on it.

        // guides.browse moved to the Guides module in PSR-1, at the same Limited floor. Same
        // name, same behaviour; only the owner changed.

        // marketplace.browse moved to the Marketplace module in B10. Same name, same Limited
        // floor, declared by MarketplaceFeatureContributor.

        // jobs.browse moved to the Jobs module in B9. Same name, same Limited floor.

        // ─── Unlocked by verification ───────────────────────────────────
        // TODO(Messaging): move to the Messaging module. Recorded as DEBT-045.
        yield return new FeatureDefinition(
            "messaging.send", Module, "Send direct messages.")
        {
            MinimumTier = AccessTier.Verified
        };

        // guides.publish moved to the Guides module in B8, which now declares it from
        // GuideFeatureContributor. Leaving it here as well is not harmless: the feature
        // catalogue refuses two modules declaring one gate, and the resulting startup failure
        // takes down every request that resolves the catalogue — including phone verification,
        // which is how every integration test creates its member.

        // marketplace.listings.create moved to the Marketplace module in B10, at its original
        // Verified floor. Leaving it here as well would not be harmless: the feature catalogue
        // refuses two modules declaring one gate, and the resulting startup failure takes down
        // every request that resolves the catalogue — phone verification included. B8 records the
        // same trap when guides.publish moved, and B9 when jobs.apply did.

        // jobs.apply moved to the Jobs module in B9, at its original Verified floor. Leaving it
        // here as well would not be harmless: the feature catalogue refuses two modules
        // declaring one gate, and the resulting startup failure takes down every request that
        // resolves the catalogue — phone verification included. B8 records the same trap when
        // guides.publish moved.

        // organizations.create moved to Community Life in B12, at its original Verified floor.
        // Leaving it here as well would not be harmless: the feature catalogue refuses two modules
        // declaring one gate, and the resulting startup failure takes down every request that
        // resolves the catalogue — phone verification included. B8 and B9 record the same trap.

        // TODO(blocked): reviews are B7's — providers:review, and every review endpoint is
        // served from that module, so the TODO that said Community was wrong. **But it cannot
        // move**, and PSR-1 found out by trying.
        //
        // ProviderArchitectureTests.Every_permission_and_feature_is_namespaced_to_this_module
        // requires every gate B7 declares to start with `providers.`. This one starts with
        // `reviews.` — it names a bounded context that never became a module — and the gate
        // cannot be renamed because the mobile client switches on the exact string.
        //
        // Widening B7's rule to admit one legacy name was the other option and was refused:
        // the platform's own doctrine is that when a name-based guard fires on a name used
        // honestly, you rename rather than widen — and where you cannot rename, the entry
        // stays where it is with the reason written down. Recorded as DEBT-045.
        yield return new FeatureDefinition(
            "reviews.post", Module, "Post a review.")
        {
            MinimumTier = AccessTier.Verified
        };

        // community.post moved to the Community module in PSR-1, at its original Verified
        // floor. Same name, same behaviour; only the owner changed.
    }
}
