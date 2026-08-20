using System.Collections.Concurrent;
using Diaspora.Operations.Telemetry;
using Diaspora.Operations.Workers;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Search;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Search;

/// <summary>
/// The background indexing hook the phase brief asks for.
/// <para>
/// Walks every registered <see cref="ISearchSynchronizer"/> on the queue policy's cadence, asks each
/// for what changed since its watermark, and hands the documents to the indexer. Does nothing at all
/// while no real provider is registered — which is the shipped state — so it costs one availability
/// check per interval until an adapter arrives.
/// </para>
/// <para>
/// <b>The watermark is advanced only after a batch is written.</b> Advancing it first is the obvious
/// bug and the expensive one: an indexer that crashes mid-batch would skip every document in it
/// forever, and the drift would be invisible until somebody searched for one of them.
/// </para>
/// </summary>
public sealed class SearchIndexingWorker : PlatformWorker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _watermarks = new(StringComparer.Ordinal);
    private readonly IServiceProvider _services;
    private readonly IClock _clock;

    public SearchIndexingWorker(
        IServiceProvider services,
        IOperationalPolicyCatalog policies,
        IClock clock,
        ILogger<SearchIndexingWorker> logger)
        : base("search-indexing", "Platform", policies, logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clock);

        _services = services;
        _clock = clock;
    }

    protected override string QueuePolicyName => OperationalPolicyNames.Queue.SearchIndexing;

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();

        var provider = scope.ServiceProvider.GetService<ISearchProvider>();

        if (provider is null ||
            !await provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            // No engine. Every module is serving search from its own database-backed default and
            // there is nothing to keep in step.
            return 0;
        }

        var indexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();
        var synchronizers = scope.ServiceProvider.GetServices<ISearchSynchronizer>().ToArray();

        if (synchronizers.Length == 0)
        {
            return 0;
        }

        var indexed = 0;
        var batchSize = Policy.BatchSize;

        foreach (var synchronizer in synchronizers)
        {
            var startedAt = _clock.UtcNow;
            var since = _watermarks.GetValueOrDefault(synchronizer.Index, DateTimeOffset.MinValue);
            var batch = new List<SearchDocument>(batchSize);

            await foreach (var document in synchronizer
                .EnumerateChangedAsync(since, cancellationToken)
                .ConfigureAwait(false))
            {
                batch.Add(document);

                if (batch.Count < batchSize)
                {
                    continue;
                }

                await indexer.IndexAsync(batch, cancellationToken).ConfigureAwait(false);
                indexed += batch.Count;
                batch.Clear();
            }

            if (batch.Count > 0)
            {
                await indexer.IndexAsync(batch, cancellationToken).ConfigureAwait(false);
                indexed += batch.Count;
            }

            // Advanced to when this pass *started*, not to when it finished. A row written while the
            // pass was running has a timestamp inside the window and would be skipped by a watermark
            // set to the end. Re-sending a handful of documents costs nothing — index writes are
            // idempotent by version.
            _watermarks[synchronizer.Index] = startedAt;

            OperationsTelemetry.SearchIndexedDocuments.Add(indexed,
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Module, synchronizer.Module),
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, synchronizer.Index));
        }

        return indexed;
    }

    /// <summary>
    /// Rewinds an index's watermark so the next pass re-sends everything after that point.
    /// <para>
    /// The recovery lever. After a restore, a mapping change, or a discovery that the index has
    /// drifted, an operator rewinds rather than rebuilding — a full rebuild of a large index during
    /// an incident is usually the more expensive of the two mistakes available.
    /// </para>
    /// </summary>
    public void Rewind(string index, DateTimeOffset to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);

        _watermarks[index] = to;
    }

    /// <summary>The current watermarks, for the operational dashboard.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Watermarks =>
        new Dictionary<string, DateTimeOffset>(_watermarks, StringComparer.Ordinal);
}
