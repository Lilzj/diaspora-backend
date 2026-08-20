using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Creator.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Reading is <see cref="AccessTier.Onboarding"/>; everything else is at least
/// <see cref="AccessTier.Limited"/>.</b> The register states the floor for
/// <c>creator:profile:read</c> outright — "from Onboarding" — and it is the right floor: the
/// creator programme is one of the things a member is deciding whether to join, and a ladder they
/// cannot see until they have finished onboarding is a ladder they will not climb.
/// </para>
/// <para>
/// <b>Taking part is <see cref="AccessTier.Limited"/>, and deliberately not
/// <see cref="AccessTier.Verified"/>.</b> B2.0.1's rule is that a tier gates <em>privilege over
/// other members</em>, not progression. Claiming a milestone, joining a challenge, finishing a
/// course and redeeming points are all acts on the caller's own record, and the creator programme's
/// own verification — which is a different state machine from Trust's — is what gates the creator
/// statuses that carry standing. Making the first useful thing a new member can do the thing they
/// cannot is the failure this platform recorded in B2.0.1 and does not repeat here.
/// </para>
/// <para>
/// <b>Reviewing is <see cref="AccessTier.Verified"/> and is separately held.</b> A verification
/// reviewer, a grant reviewer and a programme administrator each decide something on another
/// member's behalf. §29 and §30 of the phase brief require the applicant and the reviewer to be
/// separable, and three permissions rather than one is how that is expressed: a deployment can
/// give a grant panel <see cref="CreatorPermissions.GrantReview"/> without giving it the power to
/// suspend a creator.
/// </para>
/// </summary>
public sealed class CreatorPermissionContributor : IPermissionContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            CreatorPermissions.ProfileRead,
            Module,
            "Read the caller's own creator projection, and any publicly discoverable creator.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new PermissionDefinition(
            CreatorPermissions.ProfileManage,
            Module,
            "Edit the caller's own creator profile, expertise and discoverability.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CreatorPermissions.Participate,
            Module,
            "Apply, claim, join, learn, refer and redeem — every act on the caller's own record.")
        {
            MinimumTier = AccessTier.Limited
        };

        // ── The three privileged permissions ────────────────────────────────────────────────
        //
        // **Role-gated, not tier-gated, and the difference is the whole of §29 and §30.** A tier
        // floor of Verified would have granted these to *every verified member on the platform* —
        // Identity resolves a member's permissions from their tier, so "Verified and above" means
        // "everybody who finished verification". An applicant would then have been able to approve
        // their own grant, and the self-review check would have been the only thing standing between
        // a member and somebody else's decision.
        //
        // The tier floor drops to Limited for the same reason B15's staff permissions do: the role
        // is the gate, and a second floor on top of it only obscures which one is load-bearing.

        yield return new PermissionDefinition(
            CreatorPermissions.VerificationReview,
            Module,
            "Review and decide creator-programme verification applications.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = ReviewerRoles()
        };

        yield return new PermissionDefinition(
            CreatorPermissions.GrantReview,
            Module,
            "Review and decide community grant applications.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = ReviewerRoles()
        };

        yield return new PermissionDefinition(
            CreatorPermissions.ProgrammeManage,
            Module,
            "Configure milestones, challenges, grants and the reward catalogue; restrict a creator.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdministratorRoles()
        };
    }

    /// <summary>
    /// Who may decide somebody else's application.
    /// <para>
    /// Moderators and above. A grant panel is a moderation-shaped job — reading what somebody wrote
    /// and deciding about them — and the platform already has a role for that.
    /// </para>
    /// </summary>
    private static HashSet<string> ReviewerRoles() =>
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    /// <summary>
    /// Who may change the programme itself.
    /// <para>
    /// Administrators only. Editing the reward catalogue changes what everybody's points are worth,
    /// and restricting a creator is a platform decision rather than a moderation one.
    /// </para>
    /// </summary>
    private static HashSet<string> AdministratorRoles() =>
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };
}

/// <summary>
/// The module's feature gates.
/// <para>
/// Seven, so a deployment without a grant fund can turn <see cref="CreatorFeatures.Grants"/> off
/// and leave the rest running. Each gate's floor matches the permission it fronts.
/// </para>
/// </summary>
public sealed class CreatorFeatureContributor : IFeatureContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            CreatorFeatures.Programme,
            Module,
            "The creator projection, its status ladder and the creator-programme verification.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Challenges,
            Module,
            "Creator milestones and challenges.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Grants,
            Module,
            "Community grants and the applications against them.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Academy,
            Module,
            "The Creator Academy: courses, lessons, quizzes and certificates.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Referrals,
            Module,
            "The referral programme and the sign-up point bonus.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Rewards,
            Module,
            "The reward rules catalogue and point redemptions.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CreatorFeatures.Discovery,
            Module,
            "The public creator directory and the creator leaderboards.")
        {
            MinimumTier = AccessTier.Onboarding
        };
    }
}

public sealed class CreatorPolicyContributor : IPolicyContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.ProfileRead, Module, CreatorPermissions.ProfileRead);

        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.ProfileManage, Module, CreatorPermissions.ProfileManage);

        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.Participate, Module, CreatorPermissions.Participate);

        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.VerificationReview, Module, CreatorPermissions.VerificationReview);

        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.GrantReview, Module, CreatorPermissions.GrantReview);

        yield return PolicyDefinition.ForPermission(
            CreatorPolicies.ProgrammeManage, Module, CreatorPermissions.ProgrammeManage);
    }
}
