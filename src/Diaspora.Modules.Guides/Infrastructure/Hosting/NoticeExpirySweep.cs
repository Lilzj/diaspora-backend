using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Hosting;

/// <summary>
/// Announces notices whose expiry has passed.
/// <para>
/// <b>It changes no status, and that is the whole design.</b> Whether a notice is expired is derived
/// from its expiry date and needs no sweep at all — every "currently applies" listing already filters
/// on it. What a sweep is for is telling <em>other systems</em>: B13 has a message out saying "new
/// visa rules from 1 March, until 30 June", and on 1 July somebody should be able to know that
/// message is stale. The sweep publishes that fact once and marks the notice so it is never published
/// twice.
/// </para>
/// <para>
/// <b>A scope per pass, never a captured one.</b> The repository and the unit of work are scoped;
/// resolving them once into a singleton hosted service is the captive dependency that gives every
/// pass for the process lifetime the same stale <c>DbContext</c>.
/// </para>
/// <para>
/// One pass at startup, then on the interval: a deployment that has been down for a day should work
/// through its backlog immediately rather than waiting out another tick. Each pass is bounded, so a
/// long outage is worked through over several passes rather than in one transaction nobody can
/// interrupt.
/// </para>
/// </summary>
public sealed class NoticeExpirySweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<IntelligenceOptions> _options;
    private readonly ILogger<NoticeExpirySweep> _logger;

    public NoticeExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<IntelligenceOptions> options,
        ILogger<NoticeExpirySweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableExpirySweep)
        {
            return;
        }

        var interval = _options.Value.ExpirySweepInterval;

        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromMinutes(15);
        }

        using var timer = new PeriodicTimer(interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass. Returns how many notices were retired.
    /// <para>
    /// <b>Public so tests can drive it</b>, which is how every background loop on this platform is
    /// exercised: the integration suite shares one in-memory SQLite connection, and a timer firing
    /// against it mid-command is a concurrent use SQLite refuses. A test that asserted on a side
    /// effect after a timer happened to have fired would be flaky wearing a passing badge.
    /// </para>
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SweepAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
#pragma warning disable CA1031 // A failed pass must not stop the loop; the next one retries.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The official notice expiry sweep failed.");

            return 0;
        }
#pragma warning restore CA1031
    }

    private async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var notices = scope.ServiceProvider.GetRequiredService<IOfficialNoticeRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var due = await notices.ListExpiredUnannouncedAsync(
            now, _options.Value.ExpirySweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        var retired = 0;

        foreach (var notice in due)
        {
            if (notice.Expire(now))
            {
                retired++;
            }
        }

        if (retired == 0)
        {
            return 0;
        }

        // One save for the batch. The domain events raised by Expire are dispatched by the shared
        // SaveChanges pipeline and reach the outbox in the same transaction — which is what makes a
        // hundred expiries a hundred durable messages rather than a hundred chances to lose one.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        IntelligenceTelemetry.NoticesExpired.Add(retired);

        _logger.LogInformation("Retired {Count} expired official notices.", retired);

        return retired;
    }
}
