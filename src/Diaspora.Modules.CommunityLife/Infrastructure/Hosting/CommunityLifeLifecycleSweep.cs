using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Hosting;

/// <summary>
/// Moves events through the states that are functions of the clock, and settles their attendance.
/// <para>
/// <b>An event completes because its end time passed, not because somebody pressed a button.</b>
/// Nobody who has just run a festival is going to open an app and mark it finished, so a platform
/// that waited would have a permanent backlog of events that never ended and attendance figures that
/// never settled.
/// </para>
/// <para>
/// <b>The event completes before its no-shows are settled, and the order is load-bearing.</b>
/// Completion freezes the going and checked-in counts into the domain event; settling first would
/// release every absent member's place and leave going equal to checked-in, reporting perfect
/// attendance at every event on the platform. The comment is here because the two lines look
/// interchangeable and are not.
/// </para>
/// <para>
/// <b>Settled after a delay rather than at the closing bell.</b> An organizer working a door often
/// enters the last arrivals afterwards, and a sweep that ran on the hour would record them as
/// absent.
/// </para>
/// <para>
/// Every failure is caught: a sweep that threw would stop the timer and silently end all lifecycle
/// movement for the process.
/// </para>
/// </summary>
public sealed class CommunityLifeLifecycleSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly ILogger<CommunityLifeLifecycleSweep> _logger;
    private readonly IClock _clock;

    public CommunityLifeLifecycleSweep(
        IServiceScopeFactory scopes,
        IOptions<CommunityLifeOptions> options,
        ILogger<CommunityLifeLifecycleSweep> logger,
        IClock clock)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableLifecycleSweep)
        {
            _logger.LogInformation("CommunityLifeLifecycleSweep is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _options.Value.LifecycleSweepMinutes));

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

            await StartAsync(scope.ServiceProvider, cancellationToken);
            await CompleteAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A failed pass must not stop the timer. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The Community Life lifecycle sweep failed.");
        }
#pragma warning restore CA1031
    }

    /// <summary>Published events whose start time has passed become in-progress.</summary>
    private async Task StartAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var events = services.GetRequiredService<ICommunityEventRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        var now = _clock.UtcNow;
        var batchSize = Math.Clamp(_options.Value.SweepBatchSize, 1, 1_000);

        var started = await events.FindStartedAsync(now, batchSize, cancellationToken);

        if (started.Count == 0)
        {
            return;
        }

        foreach (var communityEvent in started)
        {
            var moved = communityEvent.MoveTo(EventStatus.InProgress, now);

            if (moved.IsSuccess)
            {
                events.Update(communityEvent);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Events that ended long enough ago are completed and their attendance settled.
    /// <para>
    /// See the class summary for why completion runs first.
    /// </para>
    /// </summary>
    private async Task CompleteAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var events = services.GetRequiredService<ICommunityEventRepository>();
        var rsvps = services.GetRequiredService<IEventRsvpRepository>();
        var analytics = services.GetRequiredService<IEventAnalyticsRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var now = _clock.UtcNow;
        var settleAfter = TimeSpan.FromHours(
            Math.Max(0, _options.Value.SettleAttendanceAfterHours));

        var batchSize = Math.Clamp(_options.Value.SweepBatchSize, 1, 1_000);

        var ended = await events.FindEndedAsync(now - settleAfter, batchSize, cancellationToken);

        if (ended.Count == 0)
        {
            return;
        }

        foreach (var communityEvent in ended)
        {
            var live = await rsvps.FindLiveAsync(
                communityEvent.Id, batchSize, cancellationToken);

            // Counted before anything moves, because completion is about to freeze the figures and
            // the settlement below is about to change them.
            var noShows = live.Count(rsvp => rsvp.Status == RsvpStatus.Going);

            var completed = communityEvent.Complete(now, noShows);

            if (completed.IsFailure)
            {
                continue;
            }

            events.Update(communityEvent);

            foreach (var rsvp in live)
            {
                var settled = rsvp.Settle(guids.NewSequentialGuid(), now);

                if (settled.IsSuccess)
                {
                    rsvps.Update(rsvp);
                }
            }

            var record = await analytics.FindByEventAsync(communityEvent.Id, cancellationToken);

            record?.RecordCompletion(now);

            if (noShows > 0)
            {
                CommunityLifeTelemetry.NoShows.Add(noShows);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
