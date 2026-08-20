using Diaspora.Contracts.Authorization;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// Decides, for one caller, which of a tier's requirements are met.
/// <para>
/// <b>Server-side, on every read, and never stored.</b> "Is this person verified" is a fact about
/// the caller at the moment of asking, not a property of the tier; storing it would mean a
/// requirement that was met last month still reading as met today. §19 forbids the client computing
/// one, and it could not — the account's standing is not on the device.
/// </para>
/// <para>
/// <b>Three kinds are genuinely evaluable today, and the rest answer honestly rather than
/// optimistically.</b> A <see cref="MembershipRequirementKind.Verification"/> requirement is the
/// platform's access tier, which this module reads from the presented token.
/// <see cref="MembershipRequirementKind.Invitation"/> is an existing invitation, which is B12's edge.
/// Everything else — residency, age, a referral, a document — depends on a fact the platform does not
/// yet hold about a member, and the honest answer is <b>not met</b>.
/// </para>
/// <para>
/// <b>Unmet-by-default is the safe direction and it is also the correct one.</b> Answering "met" for
/// a requirement nobody can check would let a client draw a join button that the application route
/// then refuses, which is the worst of both — and it would put a claim on the screen ("you meet this
/// organization's residency requirement") that nothing stands behind. What the member sees instead is
/// the requirement, unmet, with the organization's own wording and a route to satisfy it.
/// </para>
/// </summary>
public sealed class MembershipEligibilityEvaluator
{
    private readonly ICurrentUser _currentUser;

    public MembershipEligibilityEvaluator(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The caller's tier, as the token asserts it. Anonymous when there is no token.</summary>
    public AccessTier CallerTier =>
        _currentUser.Claims.TryGetValue(PlatformClaimTypes.AccessTier, out var raw)
            && Enum.TryParse<AccessTier>(raw, ignoreCase: true, out var tier)
                ? tier
                : AccessTier.Anonymous;

    /// <summary>Whether one requirement is met by the caller.</summary>
    public bool IsMet(TierRequirement requirement, bool holdsInvitation)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return requirement.Kind switch
        {
            MembershipRequirementKind.Verification => CallerTier >= AccessTier.Verified,
            MembershipRequirementKind.Invitation => holdsInvitation,

            // Residency, Age, Referral, Document, Other. See the class summary: the platform holds no
            // checkable fact behind any of them yet, and claiming otherwise would put an unbacked
            // assertion on a member's screen.
            _ => false
        };
    }

    /// <summary>Whether every requirement on a tier is met.</summary>
    public bool AllMet(MembershipTier tier, bool holdsInvitation)
    {
        ArgumentNullException.ThrowIfNull(tier);

        return tier.Requirements.All(requirement => IsMet(requirement, holdsInvitation));
    }
}
