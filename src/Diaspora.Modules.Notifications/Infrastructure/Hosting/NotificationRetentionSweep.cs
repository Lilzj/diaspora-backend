using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Ages out messages whose retention has passed.
/// <para>
/// <b>Expiry is a status, not a delete.</b> A viewing reminder for a viewing that has happened is no
/// longer true and should leave the inbox; the record of having sent it is what makes "we did tell
/// them" answerable a year later. The counters follow from the archived event, which carries the
/// reason.
/// </para>
/// </summary>
public sealed class NotificationRetentionSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<NotificationRetentionSweep> _logger;

    public NotificationRetentionSweep(
        IServiceScopeFactory scopes,
        IOptions<NotificationOptions> options,
        ILogger<NotificationRetentionSweep> logger)
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

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.RetentionSweepMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Expires one batch. Public so the test suite can drive it without a timer.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var notifications = services.GetRequiredService<INotificationRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var clock = services.GetRequiredService<IClock>();

            var now = clock.UtcNow;

            var expired = await notifications.ListExpiredAsync(
                now, _options.Value.RetentionBatchSize, cancellationToken);

            foreach (var notification in expired)
            {
                notification.Archive(ArchiveReason.Expired, byRecipient: false, now);
                notifications.Update(notification);
            }

            if (expired.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Expired {Count} notifications.", expired.Count);
            }

            return expired.Count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "A notification retention pass failed. The loop continues.");

            return 0;
        }
    }
}
