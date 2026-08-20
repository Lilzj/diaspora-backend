using System.Collections.Concurrent;
using Diaspora.Platform.Operations.Workers;

namespace Diaspora.Operations.Workers;

/// <summary>
/// Holds the last reported status of every worker that has started in this process.
/// <para>
/// Per-process by construction. A worker's status is a fact about one instance — replica four's
/// outbox dispatcher being stalled is not a fact about replica one — and aggregating them into a
/// single platform-wide answer would hide exactly the case an operator is looking for. The health
/// endpoint reports this instance; the dashboard aggregates across instances by scraping each.
/// </para>
/// </summary>
public sealed class WorkerRegistry : IWorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerStatus> _statuses = new(StringComparer.Ordinal);

    public IReadOnlyList<WorkerStatus> All =>
        [.. _statuses.Values.OrderBy(status => status.Name, StringComparer.Ordinal)];

    public WorkerStatus? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _statuses.GetValueOrDefault(name);
    }

    public void Report(WorkerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        _statuses[status.Name] = status;
    }
}
