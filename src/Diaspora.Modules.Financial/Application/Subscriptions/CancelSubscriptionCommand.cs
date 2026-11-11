using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>
/// Stops a subscription renewing.
/// <para>
/// Ends at the period the member has already paid for, unless staff force it immediately. Ending
/// access the instant somebody clicks cancel is taking back something that was paid for.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.SubscriptionsManage)]
[Auditable(Action = "financial.subscription.cancel")]
public sealed record CancelSubscriptionCommand : ICommand<SubscriptionResponse>
{
    public required Guid SubscriptionId { get; init; }

    public string? Reason { get; init; }

    /// <summary>End access now. Staff only; ignored for a member's own cancellation.</summary>
    public bool Immediately { get; init; }
}
