using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Extensibility.Digests;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Generates digests for members whose schedule fires this hour.
/// <para>
/// <b>Hourly, matching the granularity of the schedule itself.</b> A member asking for 8am gets it
/// within the hour; a sweep running every minute would be sixty times the work to make a daily
/// summary punctual, and nobody has ever wanted their digest at 08:00 rather than 08:00–08:59.
/// </para>
/// <para>
/// Generation is idempotent on the window, so a restart mid-hour cannot produce the same digest
/// twice — which on a digest means sending the same email twice.
/// </para>
/// </summary>
public sealed class NotificationDigestSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<NotificationDigestSweep> _logger;

    public NotificationDigestSweep(
        IServiceScopeFactory scopes,
        IOptions<NotificationOptions> options,
        ILogger<NotificationDigestSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableDigestSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.DigestSweepMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>Generates one pass. Public so the test suite can drive it without a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var generated = 0;

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var preferences = services.GetRequiredService<INotificationPreferenceRepository>();
            var generator = services.GetRequiredService<IDigestGenerator>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var now = services.GetRequiredService<IClock>().UtcNow;

            foreach (var frequency in new[] { DigestFrequency.Daily, DigestFrequency.Weekly })
            {
                var due = await preferences.ListDigestDueAsync(
                    now, frequency, _options.Value.DigestBatchSize, cancellationToken);

                foreach (var preference in due)
                {
                    var window = preference.Digest.Window;

                    var digest = await generator.GenerateAsync(
                        new DigestRequest
                        {
                            RecipientUserId = preference.UserId,
                            Frequency = frequency,
                            WindowStart = now - window,
                            WindowEnd = now,
                            Language = preference.Language,

                            // True here and false from the member's own button: an empty window
                            // recorded is what makes "why do I never get my summary" answerable.
                            RecordWhenEmpty = true
                        },
                        cancellationToken);

                    if (digest.IsSuccess)
                    {
                        generated++;
                    }
                }
            }

            if (generated > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Generated {Count} digests.", generated);
            }

            return generated;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "A digest pass failed. The loop continues.");

            return generated;
        }
    }
}
