using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Hosting;

/// <summary>
/// Concludes promotions whose window has closed.
/// <para>
/// <b>Without it, a featured flag outlives the promotion that paid for it</b> — which is the exact
/// failure a boolean-on-the-listing design has and the reason a promotion is its own aggregate. The
/// pass raises each promotion's ending event; a handler takes the flag down, but only once no other
/// live promotion on the same listing still grants it.
/// </para>
/// <para>
/// Runs on a longer interval than the others because a promotion running half an hour past its
/// window is a rounding error, while a reservation held half an hour past its window is stock
/// somebody could have bought.
/// </para>
/// </summary>
public sealed class PromotionExpirySweep : MarketplaceSweep
{
    public PromotionExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<MarketplaceOptions> options,
        ILogger<PromotionExpirySweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Math.Max(1, Options.PromotionSweepIntervalMinutes));

    protected override string Name => "promotion-expiry";

    protected override async Task<int> RunPassAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var promotions = services.GetRequiredService<IListingPromotionRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var due = await promotions.GetExpiredAsync(now, Options.SweepBatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var promotion in due)
        {
            promotion.Conclude(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return due.Count;
    }
}
