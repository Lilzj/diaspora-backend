using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
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
/// Lets risk evidence age out (RWD-14, CS-4b).
/// <para>
/// <b>What the clock decides.</b> A member's assessment is renewed when evidence arrives, and nothing
/// arrives to say a week has passed. This sweep reassesses every member above the baseline, the
/// stalest first, so a spike older than seven days or a rejection older than thirty stops counting,
/// and publishing, which High pauses, comes back. It lifts no points hold: a level that falls is not a
/// review, and the Financial Platform acts on High only. Evidence older than the longest window is
/// removed, so the table never becomes a member's history.
/// </para>
/// <para>
/// <b>Not behind <c>Trust:EnableRiskAssessment</c>.</b> It can only lower what evidence on record
/// says, so with the switch off it lets an assessment made while it was on age out, rather than
/// leaving that member's publishing paused.
/// </para>
/// <para>
/// <b>Idempotent, retry-safe and concurrency-safe</b>, as every module's sweep is: a reassessment of an
/// unchanged member renews its timestamp and announces nothing, a failed pass loses only that pass,
/// and B17's <see cref="IDistributedLock"/> keeps two instances from announcing one change twice.
/// </para>
/// </summary>
public sealed class TrustRiskSweep : BackgroundService
{
    private const string LockKey = "trust:risk";
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<TrustOptions> _options;
    private readonly ILogger<TrustRiskSweep> _logger;

    public TrustRiskSweep(
        IServiceScopeFactory scopes,
        IOptions<TrustOptions> options,
        ILogger<TrustRiskSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableRiskSweep)
        {
            _logger.LogInformation("TrustRiskSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.RiskSweepMinutes));

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

    /// <summary>One pass. Public so a test can drive it deterministically.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromMinutes(Math.Max(2, _options.Value.RiskSweepMinutes + 1)),
                cancellationToken);

        if (locks is not null && lease is null)
        {
            _logger.LogDebug("Another instance holds the Trust risk sweep lease.");

            return;
        }

        try
        {
            await SweepAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The next pass finds the same members and the same evidence.
            _logger.LogError(exception, "The Trust risk sweep failed. It will run again.");
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync();
            }
        }
    }

    private static async Task SweepAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<IClock>();
        var profiles = services.GetRequiredService<ITrustProfileRepository>();
        var signals = services.GetRequiredService<IRiskSignalRepository>();
        var assessor = services.GetRequiredService<MemberRiskAssessor>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        foreach (var member in await profiles.ListAboveBaselineRiskAsync(BatchSize, cancellationToken))
        {
            await assessor.ReassessAsync(member, cancellationToken);
        }

        await signals.RemoveOlderThanAsync(
            clock.UtcNow - RiskPolicy.LongestWindow, BatchSize, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
