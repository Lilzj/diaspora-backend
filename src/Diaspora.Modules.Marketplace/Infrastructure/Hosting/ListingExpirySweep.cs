using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Hosting;

/// <summary>
/// Expires listings whose date has passed.
/// <para>
/// <b>Expiry is reversible, which is why this can be a sweep at all.</b> A seller who extends the
/// date brings the listing straight back with its offers, its saves and its history intact — so the
/// worst a mistimed pass can do is take a listing off the market a few minutes early, rather than
/// destroy anything.
/// </para>
/// <para>
/// The aggregate re-checks the clock, so a row that was due when the batch was read and is no longer
/// due when it is processed is left alone rather than expired on stale information.
/// </para>
/// </summary>
public sealed class ListingExpirySweep : MarketplaceSweep
{
    public ListingExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<MarketplaceOptions> options,
        ILogger<ListingExpirySweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.ExpirySweepIntervalMinutes));

    protected override string Name => "listing-expiry";

    protected override async Task<int> RunPassAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var listings = services.GetRequiredService<IMarketplaceListingRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var due = await listings.GetExpiredAsync(now, Options.SweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        var expired = 0;

        foreach (var listing in due)
        {
            // The aggregate re-checks the clock and refuses a transition it cannot make, so a row
            // whose seller extended the date between the read and this line is left alone.
            if (listing.Expire(now).IsSuccess && listing.Status == Domain.Enums.ListingStatus.Expired)
            {
                expired++;
            }
        }

        if (expired > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return expired;
    }
}
