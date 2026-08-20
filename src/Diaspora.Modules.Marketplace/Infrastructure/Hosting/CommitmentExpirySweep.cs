using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Hosting;

/// <summary>
/// Lapses offers nobody answered and reservations nobody collected.
/// <para>
/// <b>The most consequential background job in the module.</b> Every reservation it fails to expire
/// is stock nobody can buy and nobody owns, held by a buyer who never turned up; every offer it
/// fails to expire is a seller who cannot tell whether they are committed. Both are the sort of
/// failure a marketplace accumulates silently until a seller with four items has none of them
/// available.
/// </para>
/// <para>
/// <b>Both run in one sweep and one pass, deliberately.</b> They fire on the same clock, they touch
/// the same listings, and separating them would mean two scopes racing to release stock on the same
/// row. The reservation half releases stock back to the listing, which is the whole reason it must
/// not be skipped.
/// </para>
/// <para>
/// Each aggregate re-checks the clock, so a row that was due at read time and is no longer due at
/// processing time is left alone.
/// </para>
/// </summary>
public sealed class CommitmentExpirySweep : MarketplaceSweep
{
    public CommitmentExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<MarketplaceOptions> options,
        ILogger<CommitmentExpirySweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.CommitmentSweepIntervalMinutes));

    protected override string Name => "commitment-expiry";

    protected override async Task<int> RunPassAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var offers = services.GetRequiredService<IListingOfferRepository>();
        var reservations = services.GetRequiredService<IListingReservationRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<IClock>();
        var guids = services.GetRequiredService<IGuidGenerator>();

        var now = clock.UtcNow;
        var touched = 0;

        touched += await ExpireOffersAsync(offers, guids, now, cancellationToken);
        touched += await ExpireReservationsAsync(reservations, guids, now, cancellationToken);

        if (touched > 0)
        {
            // One save for both halves. The reservation's status-changed event is what releases the
            // stock, and it must commit with the reservation that raised it.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return touched;
    }

    private async Task<int> ExpireOffersAsync(
        IListingOfferRepository offers,
        IGuidGenerator guids,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var due = await offers.GetLapsedAsync(now, Options.SweepBatchSize, cancellationToken);
        var expired = 0;

        foreach (var offer in due)
        {
            // Quiet and idempotent: an offer somebody answered between the read and this line
            // succeeds and does nothing, so a retried batch does not thrash.
            if (!offer.HasLapsed(now))
            {
                continue;
            }

            if (offer.Expire(now, guids.NewSequentialGuid()).IsSuccess)
            {
                expired++;
            }
        }

        return expired;
    }

    private async Task<int> ExpireReservationsAsync(
        IListingReservationRepository reservations,
        IGuidGenerator guids,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var due = await reservations.GetLapsedAsync(
            now, Options.SweepBatchSize, cancellationToken);

        var expired = 0;

        foreach (var reservation in due)
        {
            if (!reservation.HasLapsed(now))
            {
                continue;
            }

            if (reservation.Expire(now, guids.NewSequentialGuid()).IsSuccess)
            {
                expired++;
            }
        }

        return expired;
    }
}
