using Diaspora.Modules.Marketplace.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Hosting;

/// <summary>
/// The shape every Marketplace background sweep shares.
/// <para>
/// <b>Three things have to expire in this module and none of them can wait for a person.</b> A
/// listing past its date, an offer nobody answered and a reservation nobody collected are each
/// stock or a commitment held against somebody who has moved on — and the reservation sweep in
/// particular is the difference between a marketplace and a warehouse of things nobody can buy.
/// </para>
/// <para>
/// <b>A failing pass is logged and the timer continues.</b> A sweep that threw would take its
/// hosted service down and stop expiring anything for the life of the process, which is a far worse
/// failure than one bad batch. Each pass takes its own scope, so a poisoned change tracker cannot
/// outlive the pass that poisoned it.
/// </para>
/// <para>
/// <b>Every pass is bounded.</b> A batch size rather than "everything due", so a backlog is worked
/// off over several passes instead of one transaction that locks a table for a minute.
/// </para>
/// </summary>
public abstract class MarketplaceSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;

    protected MarketplaceSweep(
        IServiceScopeFactory scopes,
        IOptions<MarketplaceOptions> options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _logger = logger;
        Options = options.Value;
    }

    /// <summary>The tuned numbers a deployment may change.</summary>
    protected MarketplaceOptions Options { get; }

    /// <summary>How often this sweep runs.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>What it is called in a log line.</summary>
    protected abstract string Name { get; }

    /// <summary>
    /// One pass. Runs inside its own scope, and returns how many rows it touched so the log line
    /// says something useful rather than "ran".
    /// </summary>
    protected abstract Task<int> RunPassAsync(
        IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.EnableBackgroundSweeps)
        {
            _logger.LogInformation(
                "The marketplace {Sweep} sweep is disabled in this process.", Name);

            return;
        }

        using var timer = new PeriodicTimer(Interval);

        // A first pass on startup rather than one interval later: a process that has been down for
        // an hour comes back with an hour of expiries owing, and making somebody wait another
        // fifteen minutes for stock to be released is the opposite of what a restart should do.
        await RunSafelyAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var touched = await RunPassAsync(scope.ServiceProvider, cancellationToken);

            if (touched > 0)
            {
                _logger.LogInformation(
                    "The marketplace {Sweep} sweep processed {Count} rows.", Name, touched);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown. Nothing to report.
        }
#pragma warning disable CA1031 // A failing pass must not stop the timer. See the class summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "The marketplace {Sweep} sweep failed. The next pass will retry.",
                Name);
        }
#pragma warning restore CA1031
    }
}
