using Diaspora.Contracts.Authorization;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Decides which <see cref="CapabilityCategory"/> a permission or a feature gate belongs to.
/// <para>
/// <b>Derived, not declared</b>, and deliberately so. Adding a category property to
/// <c>PermissionDefinition</c> or <c>FeatureDefinition</c> would edit two frozen contracts,
/// require every module to restate something it has already said, and leave the two statements
/// free to disagree. Every input this classifier needs is already on the definition.
/// </para>
/// <para>
/// The classification is <b>total</b> — every definition lands in exactly one category — and
/// order-dependent: role and audience are checked before tier, because a moderation queue at
/// <see cref="AccessTier.Verified"/> is administrative, not trusted. First match wins.
/// </para>
/// </summary>
public static class CapabilityClassifier
{
    private static readonly HashSet<string> StaffRoleNames =
        UserRole.All.Where(role => role.IsStaff)
            .Select(role => role.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Vocabulary that marks a capability as participation rather than onboarding.
    /// <para>
    /// Used by the architecture test to catch the specific mistake this patch exists to
    /// prevent: a module opening a membership privilege at
    /// <see cref="AccessTier.Onboarding"/> because "it seemed harmless". A crude check, and
    /// crude is the point — it fires on the name, before anyone has to reason about intent.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> MembershipVocabulary =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "post", "comment", "react", "reaction", "share", "follow", "message",
            "messaging", "send", "publish", "feed", "browse", "moderate", "poll",
            "listing", "apply", "mention", "hashtag", "bookmark", "organization"
        };

    /// <summary>
    /// Inflections a segment may carry and still be the same word. Matched as whole suffixes on
    /// top of a whole vocabulary term, so <c>posting</c> trips the guard and <c>postcard</c>
    /// does not.
    /// </summary>
    private static readonly string[] Inflections = ["", "s", "es", "ing", "ed", "er", "ers"];

    /// <summary>Classifies a permission.</summary>
    public static CapabilityCategory Classify(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        // A service permission is authority no member can ever hold, whatever its tier says —
        // it reaches a token only through IServiceTokenIssuer. That is the ADR-002 boundary,
        // and it outranks every other consideration here.
        if (permission.Audience == PermissionAudience.Service)
        {
            return CapabilityCategory.Administrative;
        }

        if (permission.Roles.Count > 0)
        {
            return permission.Roles.All(StaffRoleNames.Contains)
                ? CapabilityCategory.Administrative
                : CapabilityCategory.Contributor;
        }

        // No tier grants it and no role names it: unreachable by any member. Grouped with
        // administrative authority because the answer to "could a member hold this?" is no.
        // An architecture test asserts none exists.
        return permission.MinimumTier is not { } tier
            ? CapabilityCategory.Administrative
            : FromTier(tier);
    }

    /// <summary>Classifies a feature gate.</summary>
    public static CapabilityCategory Classify(FeatureDefinition feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        return feature.StaffOnly
            ? CapabilityCategory.Administrative
            : FromTier(feature.MinimumTier);
    }

    /// <summary>
    /// Whether a definition's declared tier is consistent with the category it classifies into.
    /// <para>
    /// Tautological for the tier-derived categories, and that is fine — the assertion that
    /// carries weight is on the role-scoped ones, where a staff capability parked at
    /// <see cref="AccessTier.Onboarding"/> would otherwise look like an onboarding capability
    /// to every reader.
    /// </para>
    /// </summary>
    public static bool TierMatchesCategory(AccessTier tier, CapabilityCategory category)
    {
        var floor = AccessTierSemantics.FloorFor(category);
        var ceiling = AccessTierSemantics.CeilingFor(category);

        // Role-scoped categories may sit at any tier: the role is the gate.
        return floor is null || ceiling is null || (tier >= floor && tier <= ceiling);
    }

    /// <summary>Whether a subject at this tier holds capabilities of this category by progression.</summary>
    public static bool IsReachable(AccessTier tier, CapabilityCategory category) =>
        AccessTierSemantics.Admits(tier, category);

    /// <summary>
    /// Whether a name reads as a membership privilege.
    /// <para>
    /// Matches on dot- and colon-separated segments rather than substrings, so
    /// <c>media.upload.photo</c> does not trip on "post" hiding inside a longer word.
    /// </para>
    /// </summary>
    public static bool ReadsAsMembershipPrivilege(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name
            .Split(['.', ':', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsMembershipWord);
    }

    private static bool IsMembershipWord(string segment) =>
        MembershipVocabulary.Any(term => Inflections.Any(suffix =>
            segment.Length == term.Length + suffix.Length
            && segment.StartsWith(term, StringComparison.OrdinalIgnoreCase)
            && segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));

    private static CapabilityCategory FromTier(AccessTier tier) => tier switch
    {
        // Nothing declares Anonymous today. If something did, it would be reachable before an
        // account exists, and the only thing that happens before an account exists is
        // registration — which is onboarding.
        AccessTier.Anonymous => CapabilityCategory.Onboarding,
        AccessTier.Onboarding => CapabilityCategory.Onboarding,
        AccessTier.Limited => CapabilityCategory.Member,
        AccessTier.PendingVerification => CapabilityCategory.Member,
        AccessTier.Verified => CapabilityCategory.Trusted,
        _ => CapabilityCategory.Administrative
    };
}
