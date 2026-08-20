using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Infrastructure.Hosting;

/// <summary>
/// Moves properties from Published to Available when their move-in date arrives.
/// <para>
/// <b>This sweep is why the two states are worth having.</b> A flat advertised in March for a June
/// tenancy is discoverable and not yet occupiable; without something to notice that June has
/// arrived, the landlord would have to remember to come back and say so — and most would not, so
/// the property would sit unreservable while people looked at it.
/// </para>
/// <para>
/// Nothing here changes visibility, moderation or units. It moves a lifecycle state on the strength
/// of a date passing, which is the one thing a clock can honestly decide about a property.
/// </para>
/// </summary>
public sealed class HousingAvailabilitySweep : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HousingOptions> _options;
    private readonly ILogger<HousingAvailabilitySweep> _logger;

    public HousingAvailabilitySweep(
        IServiceScopeFactory scopes,
        IOptions<HousingOptions> options,
        ILogger<HousingAvailabilitySweep> logger)
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
            Math.Max(1, _options.Value.AvailabilitySweepIntervalMinutes));

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

            var listings = scope.ServiceProvider
                .GetRequiredService<IHousingListingRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var now = clock.UtcNow;

            var due = await listings.GetBecomingAvailableAsync(
                now, _options.Value.SweepBatchSize, cancellationToken);

            if (due.Count == 0)
            {
                return;
            }

            var moved = 0;

            foreach (var listing in due)
            {
                // Only where units actually remain. A property whose whole block was reserved
                // before its move-in date should become Reserved rather than Available, and the
                // aggregate's own settlement already put it there.
                if (!listing.Availability.HasUnitsRemaining)
                {
                    continue;
                }

                var result = listing.MoveTo(HousingListingStatus.Available, now);

                if (result.IsSuccess)
                {
                    moved++;
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Moved {Count} properties to available as their move-in date arrived.", moved);
        }
        catch (OperationCanceledException)
        {
            // Shutdown. Nothing to report.
        }
#pragma warning disable CA1031 // A sweep failure must not take the host down. It runs again shortly.
        catch (Exception exception)
        {
            _logger.LogError(exception, "The housing availability sweep failed.");
        }
#pragma warning restore CA1031
    }
}
