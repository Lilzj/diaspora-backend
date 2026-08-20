using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Community.Authorization;

/// <summary>
/// Declares the feature gates the module owns.
/// <para>
/// The client already has the screen these drive: a lock card naming the feature and offering
/// verification. It currently decides locally from the account state, which means the rule lives
/// in two places and only one of them is enforceable. These names are what let it ask.
/// </para>
/// </summary>
public sealed class CommunityFeatureContributor : IFeatureContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            CommunityFeatures.Feed, Module, "See the community feed.")
        {
            // Limited, not Onboarding, and this is the floor for every gate in the module.
            // An account that has not verified its phone number is not yet a member, and the
            // client's own lock card says community access follows verification — a gate that
            // opened earlier would contradict the copy the member is reading.
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Posting, Module, "Post local alerts and updates.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Composer, Module, "Create content.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Commenting, Module, "Join discussions.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Reactions, Module, "React to content.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Sharing, Module, "Share content.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Bookmarks, Module, "Save things for later.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Follows, Module, "Follow contributors.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Polls, Module, "Ask the community a question.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Mentions, Module, "Mention people.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Hashtags, Module, "Use and follow hashtags.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.AudienceControl, Module, "Choose who sees what you post.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityFeatures.ModerationQueue, Module, "Review reported content.")
        {
            MinimumTier = AccessTier.Verified
        };

        // ── Claimed from the provisional catalogue in PSR-1 ──────────────────────────────────
        //
        // Same names, same tiers, same behaviour as the host declared them with since B2. The
        // only thing that changed is who owns them, which is backend attribution and reaches no
        // client. The matching lines in ProvisionalCatalogContributor were deleted in the same
        // commit — leaving both would make two modules declare one gate, which the feature
        // catalogue refuses, and the resulting startup failure takes down every request that
        // resolves the catalogue, phone verification included. B8, B9, B10 and B12 each record
        // that trap from the other side of it.

        yield return new FeatureDefinition(
            CommunityFeatures.Browse, Module, "Browse the community feed.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityFeatures.Post, Module, "Post to the community feed.")
        {
            MinimumTier = AccessTier.Verified
        };
    }
}
