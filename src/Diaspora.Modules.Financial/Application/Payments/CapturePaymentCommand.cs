using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Takes the money.
/// <para>
/// <b>Transactional, because capture writes the intent and posts to the ledger.</b> A captured
/// intent whose ledger entries were never written is money the platform believes it has and cannot
/// account for.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "Capture writes the intent and posts the ledger entries; both or neither.")]
[Auditable(Action = "financial.payment.capture")]
public sealed record CapturePaymentCommand : ICommand<PaymentIntentResponse>
{
    public required Guid PaymentIntentId { get; init; }

    /// <summary>Major units. Null captures everything authorised.</summary>
    public decimal? Amount { get; init; }
}
