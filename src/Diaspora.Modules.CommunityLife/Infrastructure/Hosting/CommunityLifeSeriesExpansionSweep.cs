using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Hosting;

/// <summary>
/// Keeps every live series' dates ahead of the clock.
/// <para>
/// <b>A recurring event that never ends cannot be fully written, so it is written to a horizon and
/// the horizon moves.</b> Without this loop a weekly service's calendar simply stops at whatever
/// date the create request happened to reach — six months of dates, then nothing, and no error
/// anywhere to say why. That is the failure mode this sweep exists to prevent, and it is invisible
/// until the day somebody scrolls past the last one.
/// </para>
/// <para>
/// <b>One series per unit of work.</b> A pass that batched fifty series into one transaction would
/// lose all fifty to a single bad rule, and would hold a write lock over the events table for as
/// long as it took to write three thousand rows. Committing per series means a failure costs one
/// series' progress and the next pass picks it up.
/// </para>
/// <para>
/// Every failure is caught, for the reason the lifecycle sweep records: a pass that threw would stop
/// the timer and silently end all expansion for the process.
/// </para>
/// </summary>
public sealed class CommunityLifeSeriesExpansionSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly ILogger<CommunityLifeSeriesExpansionSweep> _logger;
    private readonly IClock _clock;

    public CommunityLifeSeriesExpansionSweep(
        IServiceScopeFactory scopes,
        IOptions<CommunityLifeOptions> options,
        ILogger<CommunityLifeSeriesExpansionSweep> logger,
        IClock clock)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableSeriesExpansionSweep)
        {
            _logger.LogInformation(
                "CommunityLifeSeriesExpansionSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _options.Value.SeriesExpansionSweepMinutes));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);

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

    /// <summary>One pass. Public so a test can run it without waiting on a timer.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var series = scope.ServiceProvider.GetRequiredService<IEventSeriesRepository>();
            var expansion = scope.ServiceProvider
                .GetRequiredService<EventSeriesExpansionService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var horizon = DateOnly.FromDateTime(
                _clock.UtcNow.UtcDateTime.AddDays(
                    Math.Clamp(_options.Value.SeriesHorizonDays, 1, 3_650)));

            var pending = await series.FindNeedingExpansionAsync(
                horizon,
                Math.Clamp(_options.Value.SweepBatchSize, 1, 500),
                cancellationToken);

            foreach (var candidate in pending)
            {
                var written = await expansion.ExpandAsync(candidate, cancellationToken);

                series.Update(candidate);

                // Per series. See the class summary.
                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (written.Count > 0)
                {
                    _logger.LogDebug(
                        "Wrote {Count} occurrences for series {SeriesId}.",
                        written.Count,
                        candidate.Id.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A failed pass must not stop the timer. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The Community Life series expansion sweep failed.");
        }
#pragma warning restore CA1031
    }
}
