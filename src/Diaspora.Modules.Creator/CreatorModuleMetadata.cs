namespace Diaspora.Modules.Creator;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class CreatorModuleMetadata
{
    public const string Name = "Creator";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "creator";

    // ── The module's public URL space ────────────────────────────────────────────────────────
    //
    // Six prefixes rather than one, and every one of them is the frontend's rather than this
    // platform's preference. The Phase 7 register writes each missing route out in full, and the
    // whole point of closing a gap is that the client's `/creator-gap/v0` prefix becomes these and
    // nothing else moves. A single `/api/v1/creator/...` space would have been tidier here and
    // would have cost a client release for every one of the eleven gaps.
    //
    // An architecture test asserts these are the module's only route prefixes, and that no other
    // module maps into them.

    /// <summary>
    /// The creator projection: profile, status, verification, milestones, the leaderboard and the
    /// directory. Distinct from Identity's <c>/users</c> and from B8's <c>/knowledge</c>
    /// contributor directory, which stays exactly as it is — see CREATOR-GAP-014.
    /// </summary>
    public const string CreatorsRoutePrefix = "/api/v1/creators";

    /// <summary>Challenge discovery, enrolment, progress and the per-challenge leaderboard.</summary>
    public const string ChallengesRoutePrefix = "/api/v1/challenges";

    /// <summary>Grant programmes, their server-defined forms, applications and reports.</summary>
    public const string GrantsRoutePrefix = "/api/v1/grants";

    /// <summary>The Creator Academy: sections, courses, lessons, quizzes and certificates.</summary>
    public const string AcademyRoutePrefix = "/api/v1/academy";

    /// <summary>The referral programme.</summary>
    public const string ReferralsRoutePrefix = "/api/v1/referrals";

    /// <summary>
    /// The reward rules catalogue, the redemption catalogue and redemptions.
    /// <para>
    /// <b>Not a second points balance.</b> B15 serves the balance and the history at
    /// <c>/api/v1/financial/rewards/me</c>, and nothing under this prefix returns one. The two
    /// spaces do not collide and the separation is the boundary made visible in the URL: what
    /// points are worth is B15's, what earns them and what they buy is this module's.
    /// </para>
    /// </summary>
    public const string RewardsRoutePrefix = "/api/v1/rewards";
}
