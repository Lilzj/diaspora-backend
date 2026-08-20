namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// What a member did with a recommendation.
/// <para>
/// <b>The whole quality loop of the module runs through this enum.</b> Acceptance rate, dismissal
/// rate, strategy performance and cold-start success are all ratios of these five values, and every
/// one of them is also recorded as a signal — so a member who dismisses three housing
/// recommendations in a row stops being shown housing, without anybody writing a rule that says so.
/// </para>
/// </summary>
public enum RecommendationFeedbackKind
{
    /// <summary>The card was rendered on the member's screen. The denominator of everything else.</summary>
    Viewed = 0,

    /// <summary>The member tapped it.</summary>
    Clicked = 1,

    /// <summary>
    /// The member did the thing the card was for — applied, enquired, joined, started the guide.
    /// <para>
    /// Reported by the client rather than inferred, because this module cannot see what happened
    /// after the tap and inferring acceptance from a click would make every strategy look good.
    /// </para>
    /// </summary>
    Accepted = 2,

    /// <summary>The member said "not this". Suppresses the target for the configured window.</summary>
    Dismissed = 3,

    /// <summary>The member saved it for later. A positive signal that is not yet an acceptance.</summary>
    Bookmarked = 4
}
