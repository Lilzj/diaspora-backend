using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>
/// The Country Intelligence Platform's telemetry instruments.
/// <para>
/// A third source on one module, and the three answer different operational questions: B8's counters
/// are about authoring, B8.1's about discovery, and these about official publication. An operator
/// asking "did the cyclone advisory go out" should not have to filter a stream about draft saves.
/// </para>
/// <para>
/// <b><see cref="CriticalNoticesPublished"/> is the instrument worth an alert, and it is an alert
/// on the <em>absence</em> of a fact rather than its presence.</b> A critical notice published is
/// normal. A critical notice published while the outbox is backed up is a member not being told about
/// a border closure, and the correlation between this counter and the platform's dispatch lag is the
/// signal that catches it.
/// </para>
/// </summary>
public static class IntelligenceTelemetry
{
    public const string SourceName = "Diaspora.Intelligence";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> NoticesPublished = Meter.CreateCounter<long>(
        "intelligence.notices.published", description: "Official notices made visible.");

    public static readonly Counter<long> CriticalNoticesPublished = Meter.CreateCounter<long>(
        "intelligence.notices.critical",
        description: "Critical-severity notices published. Correlate with outbox dispatch lag.");

    public static readonly Counter<long> NoticesSuperseded = Meter.CreateCounter<long>(
        "intelligence.notices.superseded", description: "Notices replaced by a later one.");

    public static readonly Counter<long> NoticesExpired = Meter.CreateCounter<long>(
        "intelligence.notices.expired", description: "Notices the retirement sweep retired.");

    public static readonly Counter<long> NoticeViews = Meter.CreateCounter<long>(
        "intelligence.notices.views", description: "Official notices opened.");

    public static readonly Counter<long> DashboardViews = Meter.CreateCounter<long>(
        "intelligence.dashboard.views", description: "Country intelligence dashboards opened.");

    public static readonly Counter<long> EmergencyViews = Meter.CreateCounter<long>(
        "intelligence.emergency.views",
        description: "Emergency information opened. A rising count is a country in trouble.");

    public static readonly Counter<long> Searches = Meter.CreateCounter<long>(
        "intelligence.searches", description: "Official notice searches executed.");

    public static readonly Counter<long> SearchesUnanswered = Meter.CreateCounter<long>(
        "intelligence.searches.unanswered",
        description: "Searches that returned nothing. The commissioning signal.");

    public static readonly Counter<long> SourcesRejected = Meter.CreateCounter<long>(
        "intelligence.sources.rejected",
        description: "Publications refused by a source validator.");

    public static readonly Counter<long> AuthorityVerifications = Meter.CreateCounter<long>(
        "intelligence.authorities.verifications",
        description: "Authority verification claims raised with Trust.");
}
