namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// The kinds of signal the Country Intelligence Platform emits.
/// <para>
/// Constants rather than an enum because they cross a seam to systems that are not .NET — B8.1's
/// reasoning verbatim. A warehouse column holding <c>emergency-viewed</c> is legible in a query
/// somebody writes two years from now; one holding <c>4</c> requires them to find this file first.
/// </para>
/// <para>
/// These deliberately mirror the counter kinds one-for-one. A signal is what a reader did; a counter
/// is the total. Naming them identically is what makes the shipped sink a two-line class and makes an
/// external warehouse's schema match the platform's own dashboard without a mapping table.
/// </para>
/// </summary>
public static class IntelligenceAnalyticsSignalKinds
{
    /// <summary>An official notice was opened.</summary>
    public const string NoticeViewed = "notice-viewed";

    /// <summary>A reader followed the official source or a related link off a notice.</summary>
    public const string NoticeClickThrough = "notice-click-through";

    /// <summary>A reader reached the end of a notice.</summary>
    public const string NoticeCompleted = "notice-completed";

    /// <summary>A country's intelligence dashboard was opened.</summary>
    public const string DashboardViewed = "dashboard-viewed";

    /// <summary>Somebody opened a mission's entry.</summary>
    public const string EmbassyViewed = "embassy-viewed";

    /// <summary>Somebody opened a country's emergency information.</summary>
    public const string EmergencyViewed = "emergency-viewed";

    /// <summary>Somebody opened a visa advisory.</summary>
    public const string VisaViewed = "visa-viewed";

    /// <summary>Somebody opened the public holiday calendar.</summary>
    public const string HolidaysViewed = "holidays-viewed";

    /// <summary>A reader crossed from a notice into a guide.</summary>
    public const string GuideCrossNavigation = "guide-cross-navigation";

    /// <summary>Official notice search was executed.</summary>
    public const string Searched = "searched";
}
