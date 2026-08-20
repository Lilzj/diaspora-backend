namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// What makes a signup a qualifying referral.
/// <para>
/// <b>The register asks for this in words as well as in code:</b> "A documented qualification rule,
/// served as <c>qualificationDescription</c> in words, plus the anti-abuse checks that can reject
/// one." <see cref="Description"/> is the words, <see cref="RequiredRecognisedActions"/> and
/// <see cref="QualificationWindowDays"/> are the rule, and the two are in one file so they cannot
/// drift into saying different things.
/// </para>
/// <para>
/// <b>The rule is deliberately about what the invitee <em>did</em>, not that they exist.</b> A
/// referral programme that paid on registration pays for registrations, and the platform would get
/// exactly what it paid for. Asking for one recognised contribution inside ninety days is the
/// smallest rule that distinguishes a member from an account.
/// </para>
/// <para>
/// <b>The anti-abuse checks are listed here and enforced in
/// <c>Application.Referrals.ReferralQualificationService</c>.</b> What a member is told when one
/// refuses is <c>CreatorErrors.ReferralNotEligible</c>'s single sentence — never which check it was.
/// </para>
/// </summary>
public static class ReferralQualificationPolicy
{
    /// <summary>Bumped whenever the rule below changes. Served with the programme.</summary>
    public const string Version = "creator-referral/1";

    /// <summary>How many recognised contributions the invitee must make.</summary>
    public const int RequiredRecognisedActions = 1;

    /// <summary>How long they have. The same window <c>CreatorLimits.ReferralQualificationDays</c> expires on.</summary>
    public const int QualificationWindowDays = CreatorLimits.ReferralQualificationDays;

    /// <summary>What a qualified referral pays, in points. Granted through B15, never here.</summary>
    public const int RewardPoints = 250;

    /// <summary>The platform's own sentence, served so the app ships no copy of the rule.</summary>
    public const string Description =
        "A referral qualifies once the person you invited joins and makes their first contribution "
        + "to the community — a guide, an answer, an event or a listing — within 90 days. Signing up "
        + "on its own is not enough.";

    /// <summary>The terms, served as the platform wrote them.</summary>
    public static readonly IReadOnlyList<string> Terms =
    [
        "You can invite as many people as you like.",
        "Each person can be referred once, by one member.",
        "Referring yourself, or an account you also control, does not qualify.",
        "A referral expires 90 days after the invitation if it has not qualified.",
        "Points are awarded once a referral qualifies, and appear on your rewards balance.",
        "We may decline a referral where our checks suggest the programme is being misused."
    ];

    /// <summary>
    /// The member-safe sentence for a referral the platform declined.
    /// <para>
    /// One sentence, and it names nothing. The register: a fraud refusal that explains itself is a
    /// map for getting round it.
    /// </para>
    /// </summary>
    public const string DeclinedReason = "This referral did not qualify for a reward.";

    /// <summary>What the member is shown while a referral is still working toward the rule.</summary>
    public const string PendingReason = "Waiting for their first contribution.";
}
