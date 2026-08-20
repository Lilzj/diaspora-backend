using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Hosting;

/// <summary>
/// Publishes guides whose scheduled instant has passed.
/// <para>
/// <b>A timer and a scope, and nothing else.</b> What publication actually writes — the aggregate
/// transition, the version snapshot, the per-guide save — lives in
/// <c>GuideScheduledPublicationService</c>, so the manual publish command and the scheduler
/// produce byte-identical results. A second copy of that sequence here is how a scheduled guide
/// ends up without the version row a manually published one gets.
/// </para>
/// <para>
/// <b>A scope per pass, never a captured one.</b> The service depends on a repository and a unit
/// of work, both scoped; resolving them once into a singleton hosted service is the captive
/// dependency that gives every pass for the process lifetime the same stale
/// <c>DbContext</c>.
/// </para>
/// <para>
/// One pass at startup, then on the interval: a deployment that has been down for an hour should
/// publish its backlog immediately rather than waiting out another tick. Each pass is bounded by
/// <see cref="GuideOptions.ScheduledPublisherBatchSize"/>, so a long outage is worked through
/// over several passes rather than in one transaction nobody can interrupt.
/// </para>
/// </summary>
public sealed class GuideScheduledPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<GuideOptions> _options;
    private readonly ILogger<GuideScheduledPublisher> _logger;

    public GuideScheduledPublisher(
        IServiceScopeFactory scopes,
        IOptions<GuideOptions> options,
        ILogger<GuideScheduledPublisher> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableScheduledPublisher)
        {
            return;
        }

        var interval = _options.Value.ScheduledPublisherInterval;

        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromMinutes(1);
        }

        using var timer = new PeriodicTimer(interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Runs one publication pass and reports how many guides went out.
    /// <para>
    /// Public so the integration harness can drive publication deterministically rather than
    /// waiting on a timer, and so an operator can trigger a pass after a backlog. Failures are
    /// logged and swallowed: one bad pass must not end the publisher for the process lifetime,
    /// which is what an escaping exception from <c>ExecuteAsync</c> would do.
    /// </para>
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var service = scope.ServiceProvider
                .GetRequiredService<GuideScheduledPublicationService>();

            var published = await service.RunOnceAsync(
                _options.Value.ScheduledPublisherBatchSize, cancellationToken);

            if (published > 0)
            {
                _logger.LogInformation(
                    "Published {Count} scheduled guides.", published);
            }

            return published;
        }
        catch (OperationCanceledException)
        {
            // Shutdown, or the caller cancelled. Not a failure and not worth a log line.
            return 0;
        }
#pragma warning disable CA1031 // One failed pass must not end the publisher for the process lifetime.
        catch (Exception exception)
        {
            _logger.LogError(
                exception, "A scheduled guide publication pass failed. The next pass will retry.");

            return 0;
        }
#pragma warning restore CA1031
    }
}
