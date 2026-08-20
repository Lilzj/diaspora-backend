using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Hosting;

/// <summary>
/// Deletes what the platform is no longer entitled to keep.
/// <para>
/// <b>The privacy floor of the module, and it is a deletion rather than a flag.</b> A signal is a
/// record of what somebody looked at; keeping it forever because storage is cheap is how a
/// personalization platform becomes a surveillance one. Rows past their retention window are
/// actually removed.
/// </para>
/// <para>
/// <b>Three windows, three different lengths, and each is a judgement.</b> Signals are kept a year
/// because that is roughly how long an interest stays true. Generated sets are kept a month, because
/// a member's recommendation history is one of the more revealing things the platform holds and the
/// only use for it is answering "why am I seeing this" while they can still remember asking.
/// Rollups are kept far longer, because they name nobody.
/// </para>
/// <para>
/// <b>Batched and repeated until the batch comes back short.</b> One statement deleting a month of
/// signals would hold a lock over the table the write path inserts into continuously.
/// </para>
/// </summary>
public sealed class RecommendationRetentionSweep : BackgroundService
{
    /// <summary>How many rows one delete statement takes. Small enough not to hold a long lock.</summary>
    private const int BatchSize = 1000;

    /// <summary>How many batches one pass runs before yielding to the next interval.</summary>
    private const int MaximumBatchesPerPass = 20;

    /// <summary>Rollups name nobody, so they outlive the rows behind them by a wide margin.</summary>
    private const int AnalyticsRetentionDays = 730;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<RecommendationRetentionSweep> _logger;

    public RecommendationRetentionSweep(
        IServiceScopeFactory scopes,
        IOptions<RecommendationOptions> options,
        ILogger<RecommendationRetentionSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableRetentionSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.RetentionSweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Deletes one pass' worth. Public so the test suite can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var clock = services.GetRequiredService<IClock>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var options = _options.Value;
            var now = clock.UtcNow;

            deleted += await SweepSignalsAsync(services, now, unitOfWork, cancellationToken);

            deleted += await SweepSetsAsync(
                services, now - options.SetRetention, unitOfWork, cancellationToken);

            deleted += await SweepFeedbackAsync(
                services, now - options.SignalRetention, unitOfWork, cancellationToken);

            deleted += await SweepAnalyticsAsync(services, now, unitOfWork, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "The recommendation retention sweep failed and will run again.");
        }

        return deleted;
    }

    /// <summary>
    /// Deletes expired signals, saving per batch.
    /// <para>
    /// The removals go through the change tracker — B1.3's rule — so each batch has to be flushed
    /// before the next is loaded. Without the save the tracker would accumulate twenty thousand
    /// deleted entities and the pass would end with one enormous statement, which is the shape the
    /// batching exists to avoid.
    /// </para>
    /// </summary>
    private static async Task<int> SweepSignalsAsync(
        IServiceProvider services,
        DateTimeOffset now,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var signals = services.GetRequiredService<ISignalRepository>();
        var deleted = 0;

        for (var pass = 0; pass < MaximumBatchesPerPass; pass++)
        {
            var removed = await signals.DeleteExpiredAsync(now, BatchSize, cancellationToken);

            if (removed == 0)
            {
                break;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            deleted += removed;

            if (removed < BatchSize)
            {
                break;
            }
        }

        return deleted;
    }

    private static async Task<int> SweepSetsAsync(
        IServiceProvider services,
        DateTimeOffset before,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var sets = services.GetRequiredService<IRecommendationSetRepository>();
        var deleted = 0;

        for (var pass = 0; pass < MaximumBatchesPerPass; pass++)
        {
            var removed = await sets.DeleteOlderThanAsync(before, BatchSize, cancellationToken);

            if (removed == 0)
            {
                break;
            }

            // Saved per batch: the sets carry owned item and contribution rows, so the removals go
            // through the change tracker and holding twenty thousand of them would be a large
            // allocation for no benefit.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            deleted += removed;

            if (removed < BatchSize)
            {
                break;
            }
        }

        return deleted;
    }

    private static async Task<int> SweepFeedbackAsync(
        IServiceProvider services,
        DateTimeOffset before,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var feedback = services.GetRequiredService<IRecommendationFeedbackRepository>();
        var deleted = 0;

        for (var pass = 0; pass < MaximumBatchesPerPass; pass++)
        {
            var removed = await feedback.DeleteOlderThanAsync(before, BatchSize, cancellationToken);

            if (removed == 0)
            {
                break;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            deleted += removed;

            if (removed < BatchSize)
            {
                break;
            }
        }

        return deleted;
    }

    private static async Task<int> SweepAnalyticsAsync(
        IServiceProvider services,
        DateTimeOffset now,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var analytics = services.GetRequiredService<IRecommendationAnalyticsRepository>();

        var before = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-AnalyticsRetentionDays));
        var removed = await analytics.DeleteOlderThanAsync(before, cancellationToken);

        if (removed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return removed;
    }
}
