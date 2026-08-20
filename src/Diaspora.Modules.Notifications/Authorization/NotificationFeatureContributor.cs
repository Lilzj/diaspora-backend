using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Notifications.Authorization;

/// <summary>
/// Declares the feature gates the Communication Platform owns.
/// <para>
/// <b>The inbox gate is at <see cref="AccessTier.Onboarding"/>, the lowest tier the platform grants
/// anything at.</b> Every other vertical gates its entry point at Limited or above. This one must
/// not: the first message a brand-new account receives is the one telling it what to do next, and
/// the second is the security alert. A gate above this would silence the platform for exactly the
/// people who most need to hear from it — including an account halfway through onboarding, which is
/// a state a member can only get out of if somebody tells them how.
/// </para>
/// <para>
/// <b>Not <c>Anonymous</c>, which was the first instinct and is wrong.</b> B2.0.1 fixed the tier
/// ladder's meaning: nothing is granted at Anonymous, because there is no authenticated subject
/// there to grant it to. Reading your own inbox needs an account; <c>Onboarding</c> is the tier that
/// says "there is an account, and it is not finished yet", which is exactly the audience.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class NotificationFeatureContributor : IFeatureContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            NotificationFeatures.Inbox, Module, "Read your notifications and settings.")
        {
            // See the class summary. Deliberately the lowest tier the platform grants anything at.
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            NotificationFeatures.Devices, Module, "Register a device for push notifications.")
        {
            // A push token is a write into somebody else's queue, so it needs an account that has
            // proved it can receive a code. Nothing above that: a member who has not verified their
            // identity still deserves to be told their password changed.
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            NotificationFeatures.Digests, Module, "Receive and generate digests.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            NotificationFeatures.Templates, Module, "Author notification templates.")
        {
            MinimumTier = AccessTier.Limited,

            // Staff-only rather than tier-gated, which is B2.0.1's distinction: a tier measures how
            // far a member has progressed, a role grants a privilege. Telling an ordinary member
            // that verifying unlocks template authoring would be telling them verification lets them
            // write messages the platform sends in its own name.
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            NotificationFeatures.Analytics, Module, "Read notification analytics.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            NotificationFeatures.Operations, Module, "Work the notification failure queue.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
