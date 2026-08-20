using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Hosting;

/// <summary>
/// Folds generated sets and member feedback into daily rollups.
/// <para>
/// <b>The rollup is what makes the analytics endpoint answerable at all.</b> "What is the acceptance
/// rate on the jobs surface this month" over raw feedback is an aggregate scan across the second
/// largest table in the schema; over rollups it is thirty indexed reads. The raw rows remain the
/// record, so a rollup can always be rebuilt from them.
/// </para>
/// <para>
/// <b>Generations are folded from a time window, feedback from an unrolled flag, and the asymmetry
/// is deliberate.</b> A set is immutable once written, so a window is sufficient and needs no extra
/// column on the busiest write path. Feedback arrives continuously and out of order — a member views
/// a card today and accepts it tomorrow — so a window would either double-count or miss, and a flag
/// is the only thing that makes the fold idempotent.
/// </para>
/// <para>
/// <b>Three breakdowns per fold: strategy, reason and target type.</b> They are the three questions
/// somebody actually asks — which strategy converts, which explanation members act on, and which
/// kind of thing is working — and each is one dimension on the same rollup row rather than three
/// tables.
/// </para>
/// </summary>
public sealed class AnalyticsRollupSweep : BackgroundService
{
    /// <summary>How many feedback rows one pass folds.</summary>
    private const int FeedbackBatchSize = 2000;

    /// <summary>How many sets one pass folds.</summary>
    private const int SetBatchSize = 2000;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<AnalyticsRollupSweep> _logger;

    public AnalyticsRollupSweep(
        IServiceScopeFactory scopes,
        IOptions<RecommendationOptions> options,
        ILogger<AnalyticsRollupSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableAnalyticsSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.AnalyticsSweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Folds one pass. Public so the test suite can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var folded = 0;

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var clock = services.GetRequiredService<IClock>();
            var analytics = services.GetRequiredService<IRecommendationAnalyticsRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var guids = services.GetRequiredService<IGuidGenerator>();

            var now = clock.UtcNow;

            folded += await FoldGenerationsAsync(services, analytics, now, cancellationToken);
            folded += await FoldFeedbackAsync(services, analytics, guids, now, cancellationToken);

            if (folded > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
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
            _logger.LogError(exception, "The analytics rollup sweep failed and will run again.");
        }

        return folded;
    }

    /// <summary>
    /// Folds the sets generated in the last completed hour.
    /// <para>
    /// The window is closed rather than up-to-now: folding the current hour would count it again on
    /// the next pass, and every generation figure would be inflated by however many passes ran while
    /// the hour was open.
    /// </para>
    /// </summary>
    private static async Task<int> FoldGenerationsAsync(
        IServiceProvider services,
        IRecommendationAnalyticsRepository analytics,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sets = services.GetRequiredService<IRecommendationSetRepository>();

        var windowEnd = new DateTimeOffset(
            now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

        var windowStart = windowEnd.AddHours(-1);

        var generated = await sets.ListGeneratedBetweenAsync(
            windowStart, windowEnd, SetBatchSize, cancellationToken);

        foreach (var set in generated)
        {
            var rollup = await analytics.GetOrOpenAsync(
                DateOnly.FromDateTime(set.GeneratedAt.UtcDateTime), set.Surface, cancellationToken);

            rollup.RecordGeneration(
                set.Items.Count,
                set.IsColdStart,
                set.PolicyExclusionCount,
                profileCompleteness: 0,
                set.GenerationMilliseconds,
                signalFreshnessHours: 0);

            analytics.Update(rollup);
        }

        return generated.Count;
    }

    private static async Task<int> FoldFeedbackAsync(
        IServiceProvider services,
        IRecommendationAnalyticsRepository analytics,
        IGuidGenerator guids,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var feedback = services.GetRequiredService<IRecommendationFeedbackRepository>();

        var pending = await feedback.ListUnrolledAsync(FeedbackBatchSize, cancellationToken);

        foreach (var entry in pending)
        {
            var rollup = await analytics.GetOrOpenAsync(
                DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime), entry.Surface, cancellationToken);

            rollup.RecordFeedback(entry.Kind, fromColdStartSet: false);

            // Three dimensions on one row. See the class summary.
            if (entry.StrategyKey is { } strategy)
            {
                Apply(rollup.Slice("strategy", strategy, guids.NewSequentialGuid()), entry.Kind);
            }

            if (entry.ReasonCode is { } reason)
            {
                Apply(rollup.Slice("reason", reason.ToString(), guids.NewSequentialGuid()), entry.Kind);
            }

            Apply(
                rollup.Slice("targettype", entry.Target.TargetType.ToString(), guids.NewSequentialGuid()),
                entry.Kind);

            entry.MarkRolledUp(now);

            analytics.Update(rollup);
            feedback.Update(entry);
        }

        return pending.Count;
    }

    private static void Apply(
        Domain.ValueObjects.AnalyticsBreakdown breakdown, RecommendationFeedbackKind kind)
    {
        switch (kind)
        {
            case RecommendationFeedbackKind.Viewed:
                breakdown.AddViews(1);
                break;
            case RecommendationFeedbackKind.Clicked:
                breakdown.AddClicks(1);
                break;
            case RecommendationFeedbackKind.Accepted:
                breakdown.AddAccepts(1);
                break;
            case RecommendationFeedbackKind.Dismissed:
                breakdown.AddDismissals(1);
                break;
            default:
                // A bookmark is counted on the rollup and not sliced. It is a positive signal that is
                // not yet an acceptance, and giving it its own dimension would invite it being read
                // as one.
                break;
        }
    }
}
