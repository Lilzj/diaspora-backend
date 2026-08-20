using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Jobs.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>The counters are chosen to answer questions about the platform's health as a labour
/// market, not about the process's health as a program.</b> Requests per second and error rates
/// are the host's job. What this module can uniquely report is whether jobs are being published,
/// whether applications are being answered, and whether offers are being accepted — and a
/// deployment where applications rise while responses do not is one where candidates are being
/// ignored at scale, which is invisible from any infrastructure dashboard.
/// </para>
/// </summary>
public static class JobTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Jobs";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Postings published. Tagged by country and category.</summary>
    public static Counter<long> JobsPublished { get; } =
        Meter.CreateCounter<long>("diaspora.jobs.published", "job", "Job postings published.");

    /// <summary>Applications submitted.</summary>
    public static Counter<long> ApplicationsSubmitted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.jobs.applications.submitted", "application", "Applications submitted.");

    /// <summary>Applications withdrawn by the candidate.</summary>
    public static Counter<long> ApplicationsWithdrawn { get; } =
        Meter.CreateCounter<long>(
            "diaspora.jobs.applications.withdrawn", "application", "Applications withdrawn.");

    /// <summary>
    /// First employer responses. Compared against submissions, this is the ghosting rate for
    /// the whole platform — the single most useful number about how members are being treated.
    /// </summary>
    public static Counter<long> ApplicationsAnswered { get; } =
        Meter.CreateCounter<long>(
            "diaspora.jobs.applications.answered", "application",
            "Applications that received a first employer response.");

    /// <summary>Interviews scheduled.</summary>
    public static Counter<long> InterviewsScheduled { get; } =
        Meter.CreateCounter<long>(
            "diaspora.jobs.interviews.scheduled", "interview", "Interviews scheduled.");

    /// <summary>Offers extended.</summary>
    public static Counter<long> OffersExtended { get; } =
        Meter.CreateCounter<long>("diaspora.jobs.offers.extended", "offer", "Offers extended.");

    /// <summary>Offers accepted. The end of the funnel.</summary>
    public static Counter<long> OffersAccepted { get; } =
        Meter.CreateCounter<long>("diaspora.jobs.offers.accepted", "offer", "Offers accepted.");

    /// <summary>Searches run. Tagged by whether a term was supplied.</summary>
    public static Counter<long> SearchesRun { get; } =
        Meter.CreateCounter<long>("diaspora.jobs.searches", "search", "Job searches executed.");

    /// <summary>How long the first response took, in hours.</summary>
    public static Histogram<double> ResponseHours { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.jobs.response.hours", "h", "Hours from application to first response.");

    /// <summary>How long a whole hire took, in days.</summary>
    public static Histogram<double> TimeToHireDays { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.jobs.time-to-hire.days", "d", "Days from application to acceptance.");
}
