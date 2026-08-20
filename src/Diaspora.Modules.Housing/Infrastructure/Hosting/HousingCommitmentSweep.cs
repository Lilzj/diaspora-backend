using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Infrastructure.Hosting;

/// <summary>
/// Expires viewings and reservations whose time has passed.
/// <para>
/// <b>The most important background service in the module.</b> A reservation nobody expires is a
/// home nobody can let: the units stay held, the listing stays out of discovery, and the member who
/// stopped replying has effectively removed a property from the market permanently. There is no
/// user-facing path that fixes this — the landlord can cancel, but only if they notice.
/// </para>
/// <para>
/// <b>Every reservation it expires releases its units through the same guard a cancellation
/// uses.</b> One settlement path, whether a person or the clock ended the hold, which is what keeps
/// the counts reconcilable.
/// </para>
/// <para>
/// Batched and bounded: a pass takes at most the configured batch, so a backlog drains over several
/// passes rather than holding a transaction open across thousands of rows.
/// </para>
/// </summary>
public sealed class HousingCommitmentSweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HousingOptions> _options;
    private readonly ILogger<HousingCommitmentSweep> _logger;

    public HousingCommitmentSweep(
        IServiceScopeFactory scopes,
        IOptions<HousingOptions> options,
        ILogger<HousingCommitmentSweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableBackgroundSweeps)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _options.Value.CommitmentSweepIntervalMinutes));

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    /// <summary>
    /// One pass. Public so a test can drive it deterministically rather than waiting on a timer.
    /// </summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            await ExpireViewingsAsync(scope.ServiceProvider, cancellationToken);
            await ExpireReservationsAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown. Nothing to report.
        }
#pragma warning disable CA1031 // A sweep failure must not take the host down. It runs again shortly.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The housing commitment sweep failed.");
        }
#pragma warning restore CA1031
    }

    private async Task ExpireViewingsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var viewings = services.GetRequiredService<IHousingViewingRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var lapsed = await viewings.GetLapsedAsync(
            now, _options.Value.SweepBatchSize, cancellationToken);

        if (lapsed.Count == 0)
        {
            return;
        }

        foreach (var viewing in lapsed)
        {
            viewing.Expire(now, guids.NewSequentialGuid());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Expired {Count} lapsed housing viewings.", lapsed.Count);
    }

    /// <summary>
    /// Expires lapsed reservations and returns their units.
    /// <para>
    /// The release goes through <see cref="IHousingUnitReservationGuard"/> rather than touching the
    /// listing directly, so the sweep settles units by exactly the same path a member cancelling
    /// does. Two settlement paths would eventually disagree, and the disagreement would show up as
    /// a property nobody can let.
    /// </para>
    /// </summary>
    private async Task ExpireReservationsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var reservations = services.GetRequiredService<IHousingReservationRepository>();
        var guard = services.GetRequiredService<IHousingUnitReservationGuard>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var lapsed = await reservations.GetLapsedAsync(
            now, _options.Value.SweepBatchSize, cancellationToken);

        if (lapsed.Count == 0)
        {
            return;
        }

        var expired = 0;

        foreach (var reservation in lapsed)
        {
            var units = reservation.Units;
            var result = reservation.Expire(now, guids.NewSequentialGuid());

            if (result.IsFailure)
            {
                continue;
            }

            await guard.ReleaseAsync(
                reservation.ListingId, units, now, cancellationToken);

            expired++;

            HousingTelemetry.ReservationsAbandoned.Add(
                1, new KeyValuePair<string, object?>("outcome", "Expired"));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expired {Count} lapsed housing reservations and returned their units.", expired);
    }
}
