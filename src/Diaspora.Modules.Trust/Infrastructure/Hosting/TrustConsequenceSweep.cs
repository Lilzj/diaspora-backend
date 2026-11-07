using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Moderation;
using Diaspora.Modules.Trust.Options;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Trust.Infrastructure.Hosting;

/// <summary>
/// Applies a removal's consequences once its appeal window has closed with no appeal (7 Nov 2026).
/// <para>
/// A removal costs the owner nothing until it stands. This is what notices that it now does: hourly,
/// under a distributed lock so two machines never apply the same consequence, a batch at a time. An
/// appeal refused applies them at once, from the appeal handler, not from here.
/// </para>
/// </summary>
public sealed class TrustConsequenceSweep : BackgroundService
{
    private const string LockKey = "trust:consequences";
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<TrustOptions> _options;
    private readonly ILogger<TrustConsequenceSweep> _logger;

    public TrustConsequenceSweep(
        IServiceScopeFactory scopes,
        IOptions<TrustOptions> options,
        ILogger<TrustConsequenceSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableConsequenceSweep)
        {
            _logger.LogInformation("TrustConsequenceSweep is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.Value.ConsequenceSweepMinutes)));

        while (!stoppingToken.IsCancellationRequested)
        {
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

            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>One pass. Public so tests can drive it without waiting an hour.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(2, _options.Value.ConsequenceSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            return 0;
        }

        try
        {
            var cases = scope.ServiceProvider.GetRequiredService<IModerationItemRepository>();
            var consequences = scope.ServiceProvider.GetRequiredService<ModerationConsequences>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

            var due = await cases.ListConsequencesDueAsync(now, BatchSize, cancellationToken);

            foreach (var item in due)
            {
                await consequences.ApplyAsync(item, afterAppeal: false, now, cancellationToken);
            }

            if (due.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Applied the consequences of {Count} removals whose appeal window had closed.", due.Count);
            }

            return due.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The moderation consequence sweep failed; it will try again next pass.");
            return 0;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }
}
