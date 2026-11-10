using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// The caller's subscription, if they hold one.
/// <para>
/// Returns null rather than 404 when they hold none. "No subscription" is the free plan, which is a
/// real answer rather than a missing resource.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.SubscriptionsRead)]
public sealed record GetMySubscriptionQuery : IQuery<SubscriptionResponse?>;
