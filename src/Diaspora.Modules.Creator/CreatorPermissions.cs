namespace Diaspora.Modules.Creator;

/// <summary>
/// The permissions the Creator Platform owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots,
/// see <see cref="CreatorPolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/CreatorContributors</c>.
/// </para>
/// <para>
/// <b>The split runs three ways, not two.</b> Reading a creator's own dashboard is
/// <see cref="ProfileRead"/> and is available from Onboarding, because the register is explicit
/// that <c>creator:profile:read</c> comes "from Onboarding" — a member who has just arrived can be
/// told what the creator programme is before they can take part in it. Changing anything about
/// their own creator record is <see cref="ProfileManage"/>. Deciding somebody else's application,
/// grant or programme configuration is a separate, privileged permission, because a reviewer is in
/// authority over another member.
/// </para>
/// <para>
/// <b>A permission here is a necessary condition and never a sufficient one.</b> A restricted or
/// suspended creator holds <see cref="ProfileManage"/> in their token and is still refused the
/// fields the platform prohibits — that decision is made on the record, on every request, and a
/// token cannot carry it.
/// </para>
/// </summary>
public static class CreatorPermissions
{
    private const string Prefix = "creator";

    // ── The member's own creator record ─────────────────────────────────────────────────────

    /// <summary>Read the caller's own creator projection, and any public creator.</summary>
    public const string ProfileRead = Prefix + ":profile:read";

    /// <summary>Edit the caller's own creator profile and discoverability.</summary>
    public const string ProfileManage = Prefix + ":profile:manage";

    /// <summary>
    /// Take part: apply for creator verification, claim a milestone, join a challenge, apply for a
    /// grant, learn, refer, redeem. Every one of them is an act on the caller's own record.
    /// </summary>
    public const string Participate = Prefix + ":programme:participate";

    // ── Privileged ──────────────────────────────────────────────────────────────────────────

    /// <summary>Review and decide creator-programme verification applications.</summary>
    public const string VerificationReview = Prefix + ":verification:review";

    /// <summary>Review and decide grant applications.</summary>
    public const string GrantReview = Prefix + ":grant:review";

    /// <summary>
    /// Run the programme: milestone and challenge configuration, the reward catalogue, grant
    /// programmes, and restricting or suspending a creator.
    /// </summary>
    public const string ProgrammeManage = Prefix + ":programme:manage";

    /// <summary>
    /// Work the redemption queue: fulfil what a member redeemed, or reject it and refund their
    /// points. Separate from <see cref="ProgrammeManage"/> so a deployment can hand fulfilment to
    /// whoever delivers placements without also handing them the catalogue.
    /// </summary>
    public const string RewardsManage = Prefix + ":rewards:manage";
}
