using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>
/// The Knowledge Directory's telemetry instruments.
/// <para>
/// A source of its own rather than B8's, even though the same assembly ships both. The two answer
/// different operational questions — B8's counters are about authoring and publication, these are
/// about discovery — and an operator wanting to know whether the country pages are being used
/// should not have to filter a stream about draft saves. It also means a deployment can sample them
/// differently: discovery is high-volume and authoring is not.
/// </para>
/// <para>
/// <c>KnowledgeSearchesUnanswered</c> is the one instrument here worth an alert. A rising count is
/// not an error — it is the platform being told, at volume, that people are asking for something
/// nobody has written.
/// </para>
/// </summary>
public static class KnowledgeTelemetry
{
    public const string SourceName = "Diaspora.Knowledge";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> DirectoryViews = Meter.CreateCounter<long>(
        "knowledge.directory.views", description: "Country knowledge directories opened.");

    public static readonly Counter<long> CategoryViews = Meter.CreateCounter<long>(
        "knowledge.category.views", description: "Knowledge categories opened.");

    public static readonly Counter<long> GuideDiscoveries = Meter.CreateCounter<long>(
        "knowledge.guides.discovered", description: "Guides opened from the directory.");

    public static readonly Counter<long> Searches = Meter.CreateCounter<long>(
        "knowledge.searches", description: "Knowledge searches executed.");

    public static readonly Counter<long> SearchesUnanswered = Meter.CreateCounter<long>(
        "knowledge.searches.unanswered",
        description: "Knowledge searches that returned nothing. The commissioning signal.");

    public static readonly Counter<long> DirectoryCurations = Meter.CreateCounter<long>(
        "knowledge.curations", description: "Editorial changes to a directory, category or path.");

    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "knowledge.cache.hits", description: "Composed directory reads served from cache.");

    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "knowledge.cache.misses", description: "Composed directory reads that had to be built.");
}
