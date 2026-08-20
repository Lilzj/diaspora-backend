using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Trust.Authorization;

/// <summary>
/// Declares the permissions the Trust module owns.
/// <para>
/// All of them, including the staff ones — the B2 arrangement put verification permissions in
/// Identity, which then held authority over a decision it could not reason about. B2.6 exists
/// to correct that, and this file is the correction taking effect: Identity now knows nothing
/// about what reviewing a verification means.
/// </para>
/// <para>
/// Note the tier bands. Submitting a verification is deliberately available from
/// <see cref="AccessTier.Limited"/> — a member who cannot yet do anything is exactly the
/// member who needs to verify — while confirming someone else's contribution requires
/// <see cref="AccessTier.Verified"/>, because an unverified account vouching for content is
/// the cheapest possible way to manufacture trust.
/// </para>
/// </summary>
public sealed class TrustPermissionContributor : IPermissionContributor
{
    public string Module => TrustModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            TrustPermissions.ReadSelf, Module,
            "Read your own trust profile, verifications, appeals and reputation.")
        {
            MinimumTier = AccessTier.Limited,

            // A restricted member must still be able to see why they are restricted and what
            // they can do about it. Removing that would be locking someone out of the room
            // where their appeal is heard.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            TrustPermissions.SubmitVerification, Module,
            "Submit verifications and attach supporting documents.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            TrustPermissions.SubmitAppeal, Module,
            "Appeal a trust decision.")
        {
            MinimumTier = AccessTier.Limited,

            // The whole point of an appeal is that the member disagrees with the restriction.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            TrustPermissions.ReadPublic, Module,
            "Read another member's public trust standing.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            TrustPermissions.Confirm, Module,
            "Confirm the accuracy of another member's contribution.")
        {
            MinimumTier = AccessTier.Verified
        };

        // The staff permissions carry both a role and a tier. The role is the obvious half;
        // the tier is the half worth stating, because the catalogue applies both — a
        // moderator whose own residency verification lapsed drops below Verified and stops
        // being able to judge other people's. Someone who cannot currently hold their own
        // standing should not be ruling on anyone else's.
        yield return new PermissionDefinition(
            TrustPermissions.ReviewVerification, Module,
            "Review verification submissions.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            TrustPermissions.ReviewAppeal, Module,
            "Review appeals against trust decisions.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            TrustPermissions.Moderate, Module,
            "Work the moderation queue.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Moderator", "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            TrustPermissions.ManageQueue, Module,
            "Assign and escalate moderation queue items.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };

        yield return new PermissionDefinition(
            TrustPermissions.ManageBadges, Module,
            "Award or revoke a badge outside the automatic rules.")
        {
            MinimumTier = AccessTier.Verified,
            Roles = new HashSet<string> { "Admin", "SuperAdmin" }
        };
    }
}

/// <summary>
/// Declares the Trust module's feature gates.
/// <para>
/// Gates tell a client what to show; the permissions above decide what the server will do.
/// The pairing matters here more than in most modules: the trust centre must be visible to a
/// member who cannot yet do anything in it, because it is the screen that tells them how to
/// change that.
/// </para>
/// </summary>
public sealed class TrustFeatureContributor : IFeatureContributor
{
    public string Module => TrustModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            TrustFeatures.TrustCenter, Module,
            "See your verification status, badges and trust journey.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.DiasporaVerification, Module,
            "Verify that you live outside your country of origin.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.IdentityVerification, Module,
            "Verify your identity with a government document.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.CountryVerification, Module,
            "Verify your residency in a country community.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.Appeals, Module,
            "Appeal a trust or moderation decision.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.Reputation, Module,
            "See your reputation across contribution, verification and trust.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            TrustFeatures.ModerationQueue, Module,
            "Work the community moderation queue.")
        {
            MinimumTier = AccessTier.Verified
        };
    }
}

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// The kernel validates at startup that every permission named here exists, so a typo fails
/// the boot rather than silently denying a route forever.
/// </para>
/// </summary>
public sealed class TrustPolicyContributor : IPolicyContributor
{
    public string Module => TrustModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ReadSelf, Module, TrustPermissions.ReadSelf);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.SubmitVerification, Module, TrustPermissions.SubmitVerification);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.SubmitAppeal, Module, TrustPermissions.SubmitAppeal);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ReadPublic, Module, TrustPermissions.ReadPublic);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.Confirm, Module, TrustPermissions.Confirm);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ReviewVerification, Module, TrustPermissions.ReviewVerification);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ReviewAppeal, Module, TrustPermissions.ReviewAppeal);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.Moderate, Module, TrustPermissions.Moderate);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ManageQueue, Module, TrustPermissions.ManageQueue);

        yield return PolicyDefinition.ForPermission(
            TrustPolicies.ManageBadges, Module, TrustPermissions.ManageBadges);
    }
}

/// <summary>Subscribes the host's tracer and metrics to the module's telemetry source.</summary>
public sealed class TrustObservabilityContributor : IObservabilityContributor
{
    public string Module => TrustModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [TrustTelemetry.SourceName];

    public IEnumerable<string> Meters => [TrustTelemetry.SourceName];
}
