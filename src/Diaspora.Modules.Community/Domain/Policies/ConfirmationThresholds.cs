using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Domain.Policies;

/// <summary>
/// How many confirmations a post needs before the community considers it verified.
/// <para>
/// <b>These numbers already exist on the client</b>, in its <c>VERIFICATION_THRESHOLDS</c> map,
/// and they are rendered to members as "3 of 7 confirmations". Reproducing them server-side is
/// not duplication — it is moving enforcement to the only side that can enforce anything — but
/// the two must agree, so a unit test asserts every value in this table against the client
/// literal it mirrors.
/// </para>
/// <para>
/// The shape of the rule is the interesting part. A threshold is a <em>pair</em>: a total count
/// and a minimum number of those that must come from a verified resident. Total alone is
/// trivially gamed by a handful of new accounts; verified-only alone means a genuine emergency
/// in a neighbourhood with few verified members can never be corroborated. Requiring both is
/// what makes the signal expensive to fake and still reachable.
/// </para>
/// <para>
/// Scaling by urgency follows from what a false positive costs. Getting a shopping post wrong
/// wastes a trip; getting an emergency post wrong sends people toward or away from real danger.
/// </para>
/// </summary>
public static class ConfirmationThresholds
{
    /// <summary>The threshold for a given urgency: total confirmations, and verified ones.</summary>
    public static (int Total, int Verified) For(UrgencyLevel urgency) => urgency switch
    {
        UrgencyLevel.Emergency => (10, 4),
        UrgencyLevel.Urgent => (7, 3),
        UrgencyLevel.Normal => (3, 1),
        UrgencyLevel.Low => (3, 1),
        _ => (3, 1)
    };

    /// <summary>
    /// How many disputes make a post disputed rather than merely questioned.
    /// <para>
    /// Deliberately low, and deliberately relative: a post is disputed when disputes reach a
    /// third of its confirmations, with a floor of two. One dissenting voice is not a dispute
    /// — it is a disagreement, and a platform that lets a single member flip a corroborated
    /// safety post to "disputed" has handed that member a veto.
    /// </para>
    /// </summary>
    public static int DisputeThreshold(int confirmations) => Math.Max(2, confirmations / 3);
}
