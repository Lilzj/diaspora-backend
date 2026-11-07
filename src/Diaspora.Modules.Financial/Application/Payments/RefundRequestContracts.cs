using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// A member asking for their money back.
/// <para>
/// <b>At <c>financial:payments:manage</c>, which a verified member holds — and it moves nothing.</b>
/// B15 shipped with a staff-only refund and no way for a member to ask, so the member-facing UI was
/// read-only and pointed at support. The obvious fix, letting a member call the refund route, would
/// have made the platform's decision about whether something was delivered into the member's
/// decision. This is the other half.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.refund_request.create")]
public sealed record CreateRefundRequestCommand : ICommand<RefundRequestResponse>
{
    public required Guid PaymentIntentId { get; init; }

    /// <summary>Omit for the whole refundable remainder.</summary>
    public decimal? Amount { get; init; }

    public required string Reason { get; init; }

    public required string Note { get; init; }
}

/// <summary>Withdraws a request before anybody has answered it.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.refund_request.withdraw")]
public sealed record WithdrawRefundRequestCommand : ICommand<RefundRequestResponse>
{
    public required Guid RefundRequestId { get; init; }
}

/// <summary>The caller's refund requests.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsRead)]
public sealed record GetMyRefundRequestsQuery : IQuery<IReadOnlyList<RefundRequestResponse>>
{
    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>
/// Answers a member's refund request. Staff.
/// <para>
/// <b>An approval issues the refund in the same unit of work.</b> Approving without refunding would
/// leave a member told "approved" with no money and nothing scheduled to fix it; the two are one
/// change or neither happens.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManageAny)]
[Transactional(Reason = "An approval and the refund it produces are one change or neither.")]
[Auditable(Action = "financial.refund_request.decide")]
public sealed record DecideRefundRequestCommand : ICommand<RefundRequestResponse>
{
    public required Guid RefundRequestId { get; init; }

    public required bool Approve { get; init; }

    /// <summary>
    /// A member-safe explanation. It is shown to them, in the notice and in the app. Required, at
    /// least 10 characters, to turn a request down (7 Nov 2026); optional on an approval.
    /// </summary>
    public string? DecisionNote { get; init; }
}

/// <summary>Refund requests waiting for somebody to answer. Staff.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManageAny)]
public sealed record GetPendingRefundRequestsQuery : IQuery<IReadOnlyList<RefundRequestResponse>>
{
    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
