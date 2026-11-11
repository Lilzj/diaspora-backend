using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Stops a payment before any money moved.
/// <para>
/// Refused once captured. Cancelling a captured payment is a refund, and one method meaning both
/// would give the platform two names for an operation whose accounting is entirely different.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payment.cancel")]
public sealed record CancelPaymentCommand : ICommand<PaymentIntentResponse>
{
    public required Guid PaymentIntentId { get; init; }
}
