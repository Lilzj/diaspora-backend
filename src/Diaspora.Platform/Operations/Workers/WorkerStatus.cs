namespace Diaspora.Platform.Operations.Workers;

/// <summary>Where a worker is in its lifecycle.</summary>
public enum WorkerLifecycle
{
    /// <summary>Constructed, not started.</summary>
    Created = 0,

    /// <summary>Running its loop.</summary>
    Running = 1,

    /// <summary>Backing off after consecutive failures.</summary>
    Recovering = 2,

    /// <summary>Stopped cleanly.</summary>
    Stopped = 3,

    /// <summary>Stopped by an exception it could not recover from.</summary>
    Faulted = 4
}

/// <summary>
/// What one background worker is doing, as the health check and the dashboard see it.
/// <para>
/// Before B17 the platform ran roughly two dozen background loops — sweepers, dispatchers,
/// retention jobs — and none of them reported anything. A stalled sweep was discovered by noticing
/// that something downstream had stopped changing, which is a fine way to find out hours later.
/// </para>
/// </summary>
/// <param name="Name">Unique worker name.</param>
/// <param name="Module">Owning module.</param>
/// <param name="Lifecycle">Where it is.</param>
/// <param name="StartedAt">When the loop started.</param>
/// <param name="LastRunAt">When the last iteration began. Null before the first.</param>
/// <param name="LastSuccessAt">When the last iteration completed without throwing.</param>
/// <param name="LastRunDuration">How long the last iteration took.</param>
/// <param name="ConsecutiveFailures">
/// Resets on any success. The number that matters: a worker with a high total failure count and a
/// recent success is noisy, and a worker with three consecutive failures is broken.
/// </param>
/// <param name="TotalRuns">Iterations since start.</param>
/// <param name="ItemsProcessed">Whatever the worker counts as an item, since start.</param>
/// <param name="LastError">The last exception's message. Never its stack trace.</param>
/// <param name="Concurrency">Consumers currently running, for a worker that scales itself.</param>
public sealed record WorkerStatus(
    string Name,
    string Module,
    WorkerLifecycle Lifecycle,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastRunAt,
    DateTimeOffset? LastSuccessAt,
    TimeSpan LastRunDuration,
    int ConsecutiveFailures,
    long TotalRuns,
    long ItemsProcessed,
    string? LastError,
    int Concurrency);

/// <summary>
/// Every worker in the process, and what it is doing.
/// <para>
/// A worker registers itself when it starts. That is deliberately not a registration-time list: a
/// worker that is registered in the container and never started is precisely the failure this is
/// meant to surface, and a list built from the container could not tell the difference.
/// </para>
/// </summary>
public interface IWorkerRegistry
{
    /// <summary>Current status of every worker that has started.</summary>
    IReadOnlyList<WorkerStatus> All { get; }

    /// <summary>One worker's status, or null if it has never started.</summary>
    WorkerStatus? Find(string name);

    /// <summary>Called by a worker as its state changes.</summary>
    void Report(WorkerStatus status);
}
