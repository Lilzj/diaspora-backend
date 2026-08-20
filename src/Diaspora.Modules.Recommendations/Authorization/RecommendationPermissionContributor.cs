using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Recommendations.Authorization;

/// <summary>
/// Declares the permissions the Recommendation Platform owns.
/// <para>
/// <b>Every member-facing permission survives restriction, and that is a deliberate and slightly
/// unusual choice.</b> A suspended member keeps their feed and, more importantly, keeps the controls
/// over it: the ability to turn personalization off is a privacy right rather than a privilege, and
/// suspending somebody's account should not suspend their ability to tell the platform to stop
/// profiling them.
/// </para>
/// <para>
/// <b>Every definition is constructed inline, and PSR-1 rewrote this file to make that true.</b>
/// B14 originally declared these through two private <c>Member(key, …)</c> and <c>Staff(key, …)</c>
/// helpers, which read better and cost the platform three analyzer rules. <c>DSP5002</c>,
/// <c>DSP5004</c> and <c>DSP5006</c> resolve the key through the semantic model at the <c>new</c>
/// site — and at that site the first argument was a <em>parameter</em>, so
/// <c>GetConstantValue</c> returned null and every key in this file was silently exempt from all
/// three. B1.4.1 recorded it as F8 and named this fix as the cheaper of the two ways to close it.
/// The repetition below is the price of the rules seeing what is written here, and it is worth
/// paying: thirteen of these keys are the deferred F1 finding, and until now no rule had ever
/// reported one of them.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class RecommendationPermissionContributor : IPermissionContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    /// <summary>
    /// A member permission is granted from <see cref="AccessTier.Onboarding"/> and kept under
    /// restriction; a staff permission opens at <see cref="AccessTier.Limited"/> to two roles.
    /// The shape repeats on every declaration so the key stays a constant the analyzers can read.
    /// </summary>
    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        // ── The member's own surface ─────────────────────────────────────────────────────────

        yield return new PermissionDefinition(
            RecommendationPermissions.FeedRead, Module, "See your recommendations.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.SignalsRecord, Module, "Record what you looked at.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.ProfileRead, Module,
            "See what the platform believes about you.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.ProfileManage, Module, "Refresh or rebuild your profile.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.PreferencesRead, Module,
            "Read your personalization settings.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.PreferencesManage, Module,
            "Change your personalization settings.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.FeedbackRecord, Module,
            "Dismiss, accept or save a recommendation.")
        {
            MinimumTier = AccessTier.Onboarding,
            GrantedWhenRestricted = true
        };

        // ── What everybody else sees ─────────────────────────────────────────────────────────

        yield return new PermissionDefinition(
            RecommendationPermissions.StrategiesRead, Module, "Read the ranking configuration.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.StrategiesManage, Module,
            "Retune how recommendations are ranked.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.PoliciesRead, Module, "Read the recommendation policies.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.PoliciesManage, Module, "Change what may be recommended.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.AnalyticsRead, Module, "Read recommendation analytics.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.CandidatesRead, Module, "Inspect the candidate projection.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };

        yield return new PermissionDefinition(
            RecommendationPermissions.OperationsRun, Module, "Run a sweep or a rebuild by hand.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles()
        };
    }

    /// <summary>
    /// The two roles every staff permission here is granted to.
    /// <para>
    /// A fresh set per definition rather than a shared static one, for the reason B6 and B7 both
    /// recorded: a mutable value shared between two definitions is one edit away from being two
    /// definitions that disagree. This helper hides no name from an analyzer — only the roles.
    /// </para>
    /// </summary>
    private static HashSet<string> StaffRoles() =>
        new(StringComparer.Ordinal) { "Admin", "Moderator" };
}
