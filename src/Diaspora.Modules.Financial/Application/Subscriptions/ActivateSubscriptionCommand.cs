using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// Starts a subscription.
/// <para>
/// <b>Transactional: it writes the subscription and, for a paid plan, a payment intent.</b> A
/// subscription active against a payment that was never opened is access nobody paid for.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.SubscriptionsManage)]
[Transactional(Reason = "A paid subscription writes the subscription and its first payment intent.")]
[Auditable(Action = "financial.subscription.activate")]
public sealed record ActivateSubscriptionCommand : ICommand<SubscriptionResponse>
{
    public required string PlanCode { get; init; }

    public bool WithTrial { get; init; } = true;

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}
