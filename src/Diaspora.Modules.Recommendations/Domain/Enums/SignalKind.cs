namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// What kind of fact a signal records.
/// <para>
/// <b>The kind is what a signal <em>is</em>, never where it came from.</b> A member viewing a job
/// and a member viewing a guide both record <see cref="Viewed"/>; the difference between them is
/// the target type, which is a separate field. Folding the two together — <c>ViewedJob</c>,
/// <c>ViewedGuide</c>, <c>ViewedHousing</c> — is the shape this enum deliberately does not have,
/// because it would grow by one member per vertical forever and every strategy would need a
/// matching case.
/// </para>
/// <para>
/// <b>Ordered roughly by how much intent each one reveals</b>, which is also the order their default
/// weights run in. A view is a glance; an application is a decision somebody spent an evening on.
/// </para>
/// </summary>
public enum SignalKind
{
    /// <summary>The member is somewhere. Country, city or neighbourhood, from Identity or Location.</summary>
    Location = 0,

    /// <summary>The member reads or speaks a language.</summary>
    Language = 1,

    /// <summary>The member's trust standing changed. From Trust, never inferred here.</summary>
    Trust = 2,

    /// <summary>The member's verification status changed.</summary>
    Verification = 3,

    /// <summary>The member's onboarding state changed. Decides whether cold start applies.</summary>
    Onboarding = 4,

    /// <summary>The member said what they are interested in. Explicit, and weighted as such.</summary>
    Interest = 5,

    /// <summary>The member changed a personalization preference.</summary>
    Preference = 6,

    /// <summary>The member searched for something.</summary>
    Searched = 7,

    /// <summary>The member looked at something.</summary>
    Viewed = 8,

    /// <summary>The member opened a notification about something.</summary>
    NotificationOpened = 9,

    /// <summary>The member followed a notification through to something.</summary>
    NotificationClicked = 10,

    /// <summary>The member reacted to, commented on or shared something.</summary>
    Engaged = 11,

    /// <summary>The member bookmarked or saved something.</summary>
    Saved = 12,

    /// <summary>The member followed a person, community or organization.</summary>
    Followed = 13,

    /// <summary>The member joined something — a community, an organization.</summary>
    Joined = 14,

    /// <summary>The member said they would attend something, or did attend it.</summary>
    Participated = 15,

    /// <summary>The member made progress through a guide or a learning path.</summary>
    LearningProgress = 16,

    /// <summary>The member finished a guide or a learning path.</summary>
    LearningCompleted = 17,

    /// <summary>The member applied for a job, enquired about a listing, or requested a viewing.</summary>
    Applied = 18,

    /// <summary>The member transacted — made an offer, reserved, bought, or was hired.</summary>
    Transacted = 19,

    /// <summary>The member accepted a recommendation this module made.</summary>
    RecommendationAccepted = 20,

    /// <summary>The member refused a recommendation this module made. The most useful negative signal there is.</summary>
    RecommendationDismissed = 21
}
