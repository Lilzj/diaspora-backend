using Diaspora.Modules.Notifications.Application.Delivery;
using Diaspora.Modules.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Hands due deliveries to their channels, on a loop.
/// <para>
/// <b>Off in the integration suite, and for the same two reasons the outbox dispatcher is.</b> The
/// suite shares one in-memory SQLite connection, and a background loop querying it while a request
/// is mid-command is a concurrent use SQLite refuses. Second, a test that asserts on a channel's
/// side effect after a timer happens to have fired is a flaky test wearing a passing badge — the
/// suite drives <see cref="NotificationDeliveryRunner"/> directly and gets a deterministic result.
/// </para>
/// <para>
/// One scope per pass, and the runner saves per notification. A pass that failed as a whole would
/// re-attempt everything it had already delivered.
/// </para>
/// </summary>
public sealed class NotificationDeliverySweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<NotificationDeliverySweep> _logger;

    public NotificationDeliverySweep(
        IServiceScopeFactory scopes,
        IOptions<NotificationOptions> options,
        ILogger<NotificationDeliverySweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableDeliverySweep)
        {
            _logger.LogInformation(
                "The notification delivery sweep is disabled. Deliveries will only run when driven "
                + "explicitly.");

            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.DeliverySweepSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var runner = scope.ServiceProvider.GetRequiredService<NotificationDeliveryRunner>();
            var attempted = await runner.RunAsync(_options.Value.DeliveryBatchSize, cancellationToken);

            if (attempted > 0)
            {
                _logger.LogDebug("Attempted {Count} notification deliveries.", attempted);
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
            // One bad pass must never stop the loop. A sweep that died on a transient database
            // failure would silently stop every notification on the platform until somebody
            // restarted the host.
            _logger.LogError(exception, "A notification delivery pass failed. The loop continues.");
        }
    }
}
