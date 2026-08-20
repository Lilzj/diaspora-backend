using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Hosting;

/// <summary>
/// Retires candidates whose sources withdrew them long ago.
/// <para>
/// <b>The projection is append-mostly and would otherwise grow without bound.</b> Every job ever
/// posted, every flat ever listed, every event that has happened — the table accumulates the
/// platform's entire history of recommendable things, and only a fraction of it is recommendable
/// now. The pool query already filters on activity, so the cost of not pruning is index size rather
/// than wrong answers, which is exactly the kind of cost nobody notices until it is large.
/// </para>
/// <para>
/// <b>The delay before pruning is generous on purpose.</b> A withdrawn candidate is a source of
/// truth for the analytics — "this was recommendable until Tuesday" explains a drop in acceptance —
/// and a source that unpublishes and republishes within a day is common enough that pruning
/// immediately would mean re-projecting from scratch.
/// </para>
/// </summary>
public sealed class CandidatePruningSweep : BackgroundService
{
    /// <summary>How long a withdrawn candidate is kept. See the class summary.</summary>
    private static readonly TimeSpan WithdrawnGrace = TimeSpan.FromDays(60);

    private const int BatchSize = 500;

    private const int MaximumBatchesPerPass = 10;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<CandidatePruningSweep> _logger;

    public CandidatePruningSweep(
        IServiceScopeFactory scopes,
        IOptions<RecommendationOptions> options,
        ILogger<CandidatePruningSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableCandidateSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.CandidateSweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Prunes one pass' worth. Public so the test suite can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var pruned = 0;

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var candidates = services.GetRequiredService<IRecommendationCandidateRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var clock = services.GetRequiredService<IClock>();

            var before = clock.UtcNow - WithdrawnGrace;

            for (var pass = 0; pass < MaximumBatchesPerPass; pass++)
            {
                var removed = await candidates.DeleteWithdrawnAsync(
                    before, BatchSize, cancellationToken);

                if (removed == 0)
                {
                    break;
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);

                pruned += removed;

                if (removed < BatchSize)
                {
                    break;
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
            _logger.LogError(exception, "The candidate pruning sweep failed and will run again.");
        }

        return pruned;
    }
}
