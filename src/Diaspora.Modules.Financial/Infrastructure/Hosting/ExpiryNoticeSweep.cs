using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Warns a member before something they paid for lapses.
/// <para>
/// <b>The single highest-value gap on the frontend's Phase 6 register, and it was a missing event
/// rather than a missing screen.</b> Until this ran, the only signal a member had that a promotion or
/// a subscription was about to end was an app open at the right moment computing a countdown from a
/// date — which is not a signal at all for the members who most need it.
/// </para>
/// <para>
/// <b>It runs before the sweeps that end things.</b> The ordering between background operations that
/// touch one number is a design decision rather than an accident of two timers — B12's handbook entry
/// — and here it decides whether a member is warned that a promotion is ending or told that it has.
/// </para>
/// <para>
/// <b>Announced once per window.</b> The placement records the end date it announced; a member warned
/// on every pass learns to ignore the warning, and a member warned again after a renewal moved the
/// date is being told something that is no longer true.
/// </para>
/// </summary>
public sealed class ExpiryNoticeSweep : FinancialSweep
{
    public ExpiryNoticeSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<ExpiryNoticeSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableExpiryNoticeSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.ExpiryNoticeSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var placements = scope.ServiceProvider.GetRequiredService<IPlacementRepository>();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var policies = scope.ServiceProvider.GetRequiredService<IFinancialPolicyRepository>();
        var relay = scope.ServiceProvider.GetRequiredService<FinancialIntegrationEventRelay>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var leadDays = await LeadDaysAsync(policies, cancellationToken);
        var until = now.AddDays(leadDays);
        var handled = 0;

        foreach (var placement in await placements.ListExpiringAsync(
            now, until, Options.SweepBatchSize, cancellationToken))
        {
            if (!placement.MarkExpiryAnnounced(now))
            {
                continue;
            }

            placements.Update(placement);
            handled++;

            await relay.PublishExpiringAsync(placement, leadDays, now, cancellationToken);
        }

        foreach (var subscription in await subscriptions.ListExpiringAsync(
            now, until, Options.SweepBatchSize, cancellationToken))
        {
            if (!subscription.MarkExpiryAnnounced(now))
            {
                continue;
            }

            subscriptions.Update(subscription);
            handled++;

            await relay.PublishExpiringAsync(subscription, leadDays, now, cancellationToken);
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }

    /// <summary>
    /// How much warning to give, from policy where a deployment has stated one.
    /// <para>
    /// The configured default is the fallback rather than the rule, so a deployment can change the
    /// lead time by publishing a policy version instead of by restarting.
    /// </para>
    /// </summary>
    private async Task<int> LeadDaysAsync(
        IFinancialPolicyRepository policies, CancellationToken cancellationToken)
    {
        var candidates = await policies.ListActiveByKeyAsync(
            FinancialPolicyKeys.SubscriptionProration, cancellationToken);

        var configured = candidates
            .Select(policy => policy.Int64Parameter(FinancialPolicyParameters.ExpiryLeadDays))
            .FirstOrDefault(value => value is > 0);

        return configured is { } days
            ? (int)Math.Clamp(days, 1, 90)
            : Math.Clamp(Options.ExpiryNoticeLeadDays, 1, 90);
    }
}
