using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// What changing plan would cost and what it would change.
/// <para>
/// <b>Server-computed, and that is arithmetic rather than policy.</b> A client that multiplies a plan
/// price by the remaining fraction of a period will disagree with the server at a rounding boundary,
/// and the figure it gets wrong is the one it charges somebody.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.SubscriptionsRead)]
public sealed record PreviewSubscriptionChangeQuery : IQuery<PlanChangePreviewResponse>
{
    public required Guid SubscriptionId { get; init; }

    public required string PlanCode { get; init; }
}

/// <summary>
/// Changes plan, mid-period.
/// <para>
/// <b>An upgrade takes effect now and charges the difference; a downgrade waits for the period
/// boundary.</b> Somebody who has paid for the month keeps the month — the same reasoning that makes
/// cancellation defer rather than cut access off, and the single most common way a subscription
/// implementation is unfair.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.SubscriptionsManage)]
[Transactional(Reason = "The plan change and the payment that funds it land together.")]
[Auditable(Action = "financial.subscription.change")]
public sealed record ChangeSubscriptionPlanCommand : ICommand<SubscriptionResponse>
{
    public required Guid SubscriptionId { get; init; }

    public required string PlanCode { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// The next charge on the caller's subscription.
/// <para>
/// Served rather than forecast on the device, because the current plan price is not the answer — a
/// promotion, a trial or a repricing policy all change it.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.SubscriptionsRead)]
public sealed record GetUpcomingChargesQuery : IQuery<IReadOnlyList<UpcomingChargeResponse>>;
