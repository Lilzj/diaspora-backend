using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Entitlements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// What a member is entitled to, and how much of it they have used.
/// <para>
/// <b>The ceiling is read from the subscription and the consumption from whoever owns it.</b> A plan
/// says <c>listings.concurrent: 15</c>; only Marketplace knows how many listings somebody has open.
/// Fanning out to registered providers is the only way to answer both halves without this module
/// referencing a vertical — and a missing provider produces a null consumption rather than a zero,
/// because an empty meter reads as "none used", which is a specific claim nobody made.
/// </para>
/// <para>
/// <b>The entitlements come from the subscription, not from the plan.</b> They were copied onto the
/// subscription at activation precisely so a plan repriced or re-scoped next month does not silently
/// change what somebody is already paying for — and reading through to the plan here would undo that.
/// </para>
/// <para>
/// <b>A provider that throws is skipped, not fatal.</b> A vertical being slow or down should cost a
/// member the consumption half of a meter, not the whole entitlement snapshot the rest of their
/// screen is drawn from.
/// </para>
/// </summary>
public sealed class EntitlementResolver
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IEnumerable<IEntitlementUsageProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger<EntitlementResolver> _logger;

    public EntitlementResolver(
        ISubscriptionRepository subscriptions,
        IEnumerable<IEntitlementUsageProvider> providers,
        IClock clock,
        ILogger<EntitlementResolver> logger)
    {
        _subscriptions = subscriptions;
        _providers = providers;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EntitlementSnapshotResponse> ResolveAsync(
        Guid memberUserId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var subscription = await _subscriptions.FindActiveForSubscriberAsync(
            memberUserId, cancellationToken);

        // A subscription that has ended grants nothing, whatever its entitlement rows still say.
        // The status is authoritative and is re-read here rather than trusted from anywhere else.
        var entitlements = subscription is { GrantsAccess: true }
            ? subscription.Entitlements
            : [];

        var keys = entitlements.Select(entitlement => entitlement.Key).ToList();

        IReadOnlyDictionary<string, EntitlementUsage> usage = keys.Count == 0
            ? new Dictionary<string, EntitlementUsage>(StringComparer.Ordinal)
            : await GatherAsync(
                memberUserId,
                keys,
                subscription!.CurrentPeriod.StartsAt,
                subscription.CurrentPeriod.EndsAt,
                cancellationToken);

        return new EntitlementSnapshotResponse
        {
            Entitlements = [.. entitlements.Select(Mapping.FinancialMapper.ToResponse)],
            Counters = [.. entitlements.Select(entitlement => Counter(entitlement, usage))],
            SourcePlanCode = subscription is { GrantsAccess: true } ? subscription.PlanCode : null,
            SubscriptionId = subscription is { GrantsAccess: true } ? subscription.Id.Value : null,
            ValidUntil = subscription is { GrantsAccess: true }
                ? subscription.CurrentPeriod.EndsAt
                : null,
            ResolvedAt = now
        };
    }

    private static UsageCounterResponse Counter(
        Entitlement entitlement, IReadOnlyDictionary<string, EntitlementUsage> usage)
    {
        // A value that is not a number is a setting rather than a limit — "analytics.advanced: true"
        // has no ceiling to meter, and reporting one would invent a bar.
        var limit = long.TryParse(entitlement.Value, out var parsed) ? parsed : (long?)null;

        if (!usage.TryGetValue(entitlement.Key, out var reported))
        {
            return new UsageCounterResponse { Key = entitlement.Key, Limit = limit };
        }

        return new UsageCounterResponse
        {
            Key = entitlement.Key,
            Limit = limit,
            Consumed = reported.Consumed,
            Remaining = limit is { } ceiling ? Math.Max(0, ceiling - reported.Consumed) : null,
            AtLimitBehaviour = reported.AtLimitBehaviour,
            PeriodStart = reported.PeriodStart,
            PeriodEnd = reported.PeriodEnd
        };
    }

    private async Task<IReadOnlyDictionary<string, EntitlementUsage>> GatherAsync(
        Guid memberUserId,
        IReadOnlyList<string> keys,
        DateTimeOffset periodStart,
        DateTimeOffset? periodEnd,
        CancellationToken cancellationToken)
    {
        var gathered = new Dictionary<string, EntitlementUsage>(StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            // Only providers whose keys the member's plan actually touches. A snapshot that fanned
            // out to nine verticals to fill two meters would put nine queries on one screen.
            var wanted = keys
                .Where(key => provider.Keys.Contains(key, StringComparer.Ordinal))
                .ToList();

            if (wanted.Count == 0)
            {
                continue;
            }

            try
            {
                var reported = await provider.UsageAsync(
                    memberUserId, wanted, periodStart, periodEnd, cancellationToken);

                foreach (var usage in reported)
                {
                    gathered[usage.Key] = usage;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    exception,
                    "{Provider} could not report entitlement usage. The ceilings are still served "
                    + "and the consumption is reported as unknown.",
                    provider.GetType().Name);
            }
        }

        return gathered;
    }
}
