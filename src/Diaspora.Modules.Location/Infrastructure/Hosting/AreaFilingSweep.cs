using Diaspora.Modules.Location.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Infrastructure.Hosting;

/// <summary>
/// Files every vertical's already-written rows under the areas they belong to.
/// <para>
/// <b>The backfill that keeps itself true.</b> Filing on create was added on 16 Sep 2026, and by
/// then every home, listing, job and provider on production had been written unfiled — so the
/// map's cluster endpoint counted none of them. This walks each vertical's unfiled rows through
/// <see cref="IAreaFilingSource"/> and files what the catalogue can place. It runs on startup,
/// which is what reached the rows already on production the moment it deployed, and on an
/// interval afterwards, because an older client or an import will write unfiled rows again.
/// </para>
/// <para>
/// <b>Idempotent by construction.</b> A source only ever reads rows with no area, so a second
/// pass — or a second machine running the same pass — finds the filed rows gone and does
/// nothing to them. A row the catalogue cannot place is read again on the next pass, which is the
/// point: seeding a new town files the rows that were waiting for it without anyone remembering
/// to.
/// </para>
/// <para>
/// Every failure is caught per vertical: one vertical's bad row must not stop the others being
/// filed, and a sweep that threw would stop the timer for the life of the process.
/// </para>
/// </summary>
public sealed class AreaFilingSweep : BackgroundService
{
    /// <summary>
    /// Rows one vertical may have examined in one pass, whatever the page size. A bound on the
    /// work a single pass can do against a table nobody has sized yet.
    /// </summary>
    private const int MaximumRowsPerPass = 5_000;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<LocationSpatialOptions> _options;
    private readonly ILogger<AreaFilingSweep> _logger;

    public AreaFilingSweep(
        IServiceScopeFactory scopes,
        IOptions<LocationSpatialOptions> options,
        ILogger<AreaFilingSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableAreaFilingSweep)
        {
            _logger.LogInformation("AreaFilingSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.AreaFilingSweepMinutes));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// One pass over every vertical. Public so a test can run it without waiting on a timer.
    /// </summary>
    /// <returns>Rows filed, per resource type.</returns>
    public async Task<IReadOnlyDictionary<string, int>> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        var filed = new Dictionary<string, int>(StringComparer.Ordinal);
        var pageSize = Math.Clamp(_options.Value.AreaFilingBatchSize, 1, 500);

        // One scope to discover the sources; each vertical then gets a scope of its own per page,
        // so the change tracker never holds more than one page and a failed save in one vertical
        // cannot leave half-applied entities in another's.
        IReadOnlyList<string> resourceTypes;

        using (var discovery = _scopes.CreateScope())
        {
            resourceTypes = discovery.ServiceProvider
                .GetServices<IAreaFilingSource>()
                .Select(source => source.ResourceType)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        foreach (var resourceType in resourceTypes)
        {
            filed[resourceType] = await FileVerticalAsync(resourceType, pageSize, cancellationToken);
        }

        var total = filed.Values.Sum();

        if (total > 0)
        {
            _logger.LogInformation(
                "AreaFilingSweep filed {Total} rows: {Counts}",
                total,
                string.Join(", ", filed.Select(pair => $"{pair.Key}={pair.Value}")));
        }

        return filed;
    }

    private async Task<int> FileVerticalAsync(
        string resourceType, int pageSize, CancellationToken cancellationToken)
    {
        var skip = 0;
        var examined = 0;
        var filed = 0;

        try
        {
            while (examined < MaximumRowsPerPass)
            {
                AreaFilingBatch batch;

                using (var scope = _scopes.CreateScope())
                {
                    var source = scope.ServiceProvider
                        .GetServices<IAreaFilingSource>()
                        .First(candidate => candidate.ResourceType == resourceType);

                    batch = await source.FileUnplacedAsync(skip, pageSize, cancellationToken);
                }

                examined += batch.Examined;
                filed += batch.Filed;

                // Filed rows have left the unfiled set; the unplaced ones are still ahead of us.
                skip += batch.Unplaced;

                if (batch.Examined < pageSize)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // One vertical failing must not stop the rest. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "AreaFilingSweep failed filing {ResourceType} after {Filed} rows.",
                resourceType,
                filed);
        }
#pragma warning restore CA1031

        return filed;
    }
}
