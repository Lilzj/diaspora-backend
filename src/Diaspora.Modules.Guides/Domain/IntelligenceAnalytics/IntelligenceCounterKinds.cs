namespace Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;

/// <summary>
/// The counters a country's intelligence surface keeps.
/// <para>
/// Constants rather than an enum because they are stored as text and cross a seam to systems that
/// are not .NET — B8.1's reasoning verbatim. A warehouse column holding <c>emergency-viewed</c> is
/// legible in a query somebody writes two years from now; one holding <c>4</c> requires them to find
/// this file first.
/// </para>
/// <para>
/// <b>Every one of these is a count of events, never of people.</b> The brief asks for notice views,
/// click-through, read completion, country interest, embassy visits, emergency usage, guide
/// cross-navigation, recommendation acceptance and notification correlation — and every one of those
/// questions is answerable from totals. None of them needs to know who, and the schema is where that
/// is guaranteed rather than promised.
/// </para>
/// </summary>
public static class IntelligenceCounterKinds
{
    /// <summary>An official notice was opened.</summary>
    public const string NoticeViewed = "notice-viewed";

    /// <summary>A reader followed the official source or a related link off a notice.</summary>
    public const string NoticeClickThrough = "notice-click-through";

    /// <summary>A reader reached the end of a notice. The signal the summary was not enough.</summary>
    public const string NoticeCompleted = "notice-completed";

    /// <summary>A country's intelligence dashboard was opened. The country-interest signal.</summary>
    public const string DashboardViewed = "dashboard-viewed";

    /// <summary>Somebody opened a mission's entry in the embassy directory.</summary>
    public const string EmbassyViewed = "embassy-viewed";

    /// <summary>Somebody opened a country's emergency information.</summary>
    public const string EmergencyViewed = "emergency-viewed";

    /// <summary>Somebody opened a visa advisory.</summary>
    public const string VisaViewed = "visa-viewed";

    /// <summary>Somebody opened the public holiday calendar.</summary>
    public const string HolidaysViewed = "holidays-viewed";

    /// <summary>A reader crossed from a notice into a guide. The cross-navigation signal.</summary>
    public const string GuideCrossNavigation = "guide-cross-navigation";

    /// <summary>Official notice search was executed.</summary>
    public const string Searched = "searched";

    /// <summary>A search returned nothing. The commissioning signal, as it is in B8.1.</summary>
    public const string SearchedUnanswered = "searched-unanswered";

    /// <summary>
    /// A member opened a notice B14 recommended to them.
    /// <para>
    /// Recorded here rather than in B14 because this module is where the open happens and B14 is
    /// where the recommendation was made — and the platform's rule is that a signal is recorded by
    /// whoever observes it. B14 learns the same fact from its own published surface.
    /// </para>
    /// </summary>
    public const string RecommendationAccepted = "recommendation-accepted";

    /// <summary>
    /// A member opened a notice from a notification B13 sent.
    /// <para>
    /// The delivery-correlation signal the brief asks for, and the only honest way to get it without
    /// this module knowing what a notification is: the client says where the tap came from, and this
    /// counts it.
    /// </para>
    /// </summary>
    public const string NotificationFollowed = "notification-followed";
}
