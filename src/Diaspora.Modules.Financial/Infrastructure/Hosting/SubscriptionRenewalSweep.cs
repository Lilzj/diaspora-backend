using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Renews subscriptions whose period has ended, and ends those whose grace period has run out.
/// <para>
/// <b>A subscription that was cancelled at period end simply expires here.</b> That is what
/// "cancel" means for somebody who has already paid for the month: they keep it, and this is the
/// moment it stops.
/// </para>
/// <para>
/// <b>A renewal that needs a payment enters a grace period rather than renewing on credit.</b> The
/// sweep does not take money — it moves the subscription into the state that says "we are trying" and
/// leaves the payment to the ordinary path. A card that expired on a Friday should not remove
/// somebody's paid features before anybody could have told them.
/// </para>
/// </summary>
public sealed class SubscriptionRenewalSweep : FinancialSweep
{
    public SubscriptionRenewalSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<SubscriptionRenewalSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableSubscriptionSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.SubscriptionSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var plans = scope.ServiceProvider.GetRequiredService<ISubscriptionPlanRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var handled = 0;

        foreach (var subscription in await subscriptions.ListDueForRenewalAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            // A scheduled downgrade takes effect here and nowhere else. That is what "somebody who
            // has paid for the month keeps the month" means once it has to be code: the change was
            // priced and recorded when the member asked, and the boundary is where it lands.
            var plan = subscription.PendingPlanCode is { } pending
                ? await plans.FindByCodeAsync(pending, cancellationToken)
                    ?? await plans.FindAsync(subscription.PlanId, cancellationToken)
                : await plans.FindAsync(subscription.PlanId, cancellationToken);

            if (plan is null)
            {
                continue;
            }

            if (subscription.CancelAtPeriodEnd)
            {
                subscription.End(SubscriptionStatus.Expired, "Cancelled at period end.", now);
            }
            else if (plan.IsFree)
            {
                subscription.Renew(plan, now, () => guids.NewSequentialGuid());
            }
            else
            {
                subscription.EnterGracePeriod(now, Options.SubscriptionGracePeriodDays);
            }

            subscriptions.Update(subscription);
            handled++;
        }

        foreach (var subscription in await subscriptions.ListLapsedAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            subscription.End(SubscriptionStatus.Expired, "The grace period ran out.", now);
            subscriptions.Update(subscription);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
