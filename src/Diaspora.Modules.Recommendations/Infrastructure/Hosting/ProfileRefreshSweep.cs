using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Hosting;

/// <summary>
/// Folds accumulated signals into profiles, in the background.
/// <para>
/// <b>The alternative was building a profile on the read path, and it would have been the wrong
/// trade.</b> A member opening the app after a week away has a hundred unprocessed signals; folding
/// them while they wait puts a hundred-row scan and a write inside the request that renders their
/// home screen. Doing it here means the feed reads a profile that is at most a sweep interval stale,
/// which is a far better failure than a slow launch.
/// </para>
/// <para>
/// <b>Members with work waiting first, then the merely stale.</b> Somebody who did something
/// yesterday has more to gain from a rebuild than somebody who has not opened the app in a month, and
/// a sweep that walked the table in identifier order would spend most of its budget on the second.
/// </para>
/// <para>
/// <b>One member per unit of work.</b> A batch that saved every member together would lose all of
/// them to one concurrency conflict — and conflicts are the normal case here, because a member using
/// the app while the sweep rebuilds them is exactly what this is for.
/// </para>
/// </summary>
public sealed class ProfileRefreshSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<ProfileRefreshSweep> _logger;

    public ProfileRefreshSweep(
        IServiceScopeFactory scopes,
        IOptions<RecommendationOptions> options,
        ILogger<ProfileRefreshSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableProfileSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.ProfileSweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Folds one batch. Public so the test suite can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var processed = 0;

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var signals = services.GetRequiredService<ISignalRepository>();

            var pending = await signals.ListUsersWithUnprocessedSignalsAsync(
                options.ProfileSweepBatchSize, cancellationToken);

            foreach (var userId in pending)
            {
                // A scope per member: one member's concurrency conflict must not lose the other
                // ninety-nine. See the class summary.
                using var memberScope = _scopes.CreateScope();

                try
                {
                    var builder = memberScope.ServiceProvider
                        .GetRequiredService<IPersonalizationProfileBuilder>();

                    await builder.BuildIncrementalAsync(userId, cancellationToken);

                    await memberScope.ServiceProvider
                        .GetRequiredService<IUnitOfWork>()
                        .SaveChangesAsync(cancellationToken);

                    processed++;
                }
                catch (ConcurrencyConflictException)
                {
                    // The member changed their own profile while we rebuilt it. Theirs wins; the
                    // signals stay unprocessed and the next pass picks them up.
                    _logger.LogDebug(
                        "Profile rebuild for a member lost a race and will be retried next pass.");
                }
#pragma warning disable CA1031
                catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    _logger.LogError(
                        exception, "Folding signals into a profile failed. The sweep continued.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "The profile refresh sweep failed and will run again.");
        }

        return processed;
    }
}
