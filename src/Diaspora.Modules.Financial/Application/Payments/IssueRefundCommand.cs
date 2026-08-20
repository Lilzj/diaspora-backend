using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Gives money back.
/// <para>
/// <b>Requires the staff permission, and that is a considered restriction rather than an
/// oversight.</b> A member-initiated refund is a request, not a decision — the platform has to look
/// at whether the thing was delivered before it moves money back. The request surface belongs to
/// support tooling; this is the operation that actually moves it.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManageAny)]
[Transactional(Reason = "A refund writes the intent and posts the reversing ledger entries.")]
[Auditable(Action = "financial.payment.refund")]
public sealed record IssueRefundCommand : ICommand<PaymentIntentResponse>
{
    public required Guid PaymentIntentId { get; init; }

    /// <summary>Major units. Null refunds everything still refundable.</summary>
    public decimal? Amount { get; init; }

    public required string Reason { get; init; }

    public string? Note { get; init; }
}
