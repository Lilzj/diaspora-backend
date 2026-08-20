using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>
/// The module's telemetry instruments.
/// <para>
/// The source and meter carry the name the observability contributor declares, so the host's
/// tracer and metrics pipelines subscribe to real instruments — an architecture test fails a
/// module that declares a source name with nothing behind it.
/// </para>
/// </summary>
public static class GuideTelemetry
{
    public const string SourceName = "Diaspora.Guides";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> GuidesPublished =
        Meter.CreateCounter<long>("guides.published", description: "Guides published.");

    public static readonly Counter<long> GuideVersionsCreated =
        Meter.CreateCounter<long>("guides.versions.created", description: "Immutable guide versions written.");

    public static readonly Counter<long> GuideViewsRecorded =
        Meter.CreateCounter<long>("guides.views.recorded", description: "Guide views recorded.");

    public static readonly Counter<long> GuideSearches =
        Meter.CreateCounter<long>("guides.searches", description: "Guide search queries executed.");
}
