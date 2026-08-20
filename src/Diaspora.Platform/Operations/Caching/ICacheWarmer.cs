namespace Diaspora.Platform.Operations.Caching;

/// <summary>
/// Fills a cache family before the first request needs it.
/// <para>
/// Warming exists for one shape of problem: a cold replica joining a pool under load. Its first
/// hundred requests each miss on the same reference data and each pay a database round trip, and the
/// orchestrator — seeing the latency — adds another cold replica. Warming the families a policy
/// marks <c>WarmOnStartup</c> converts that into one query per family at startup.
/// </para>
/// <para>
/// <b>Warming must never block readiness on success.</b> An implementation that throws leaves the
/// instance cold and serving, which is exactly the pre-warming behaviour; an implementation that
/// prevents startup has made a cache outage into a deployment outage. The runner logs and continues.
/// </para>
/// </summary>
public interface ICacheWarmer
{
    /// <summary>The module this warmer belongs to. Used for ordering and for the metric label.</summary>
    string Module { get; }

    /// <summary>The cache namespace being warmed, for diagnostics.</summary>
    string CacheNamespace { get; }

    /// <summary>
    /// Loads and caches the family. Called once per process start, after seeding and before the host
    /// reports ready.
    /// </summary>
    /// <returns>How many entries were written. Reported on the operational dashboard.</returns>
    Task<int> WarmAsync(CancellationToken cancellationToken = default);
}
