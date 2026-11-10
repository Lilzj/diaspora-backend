using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Hands a payment to a gateway.
/// <para>
/// <c>ClientContext</c> is whatever the adapter told the client to send back — a token, a nonce, a
/// mandate reference. Opaque to the platform, passed through unread, and never stored.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "Authorising a one-step provider also captures, which posts to the ledger.")]
[Auditable(Action = "financial.payment.authorize")]
public sealed record AuthorizePaymentCommand : ICommand<PaymentIntentResponse>
{
    public required Guid PaymentIntentId { get; init; }

    /// <summary>Opaque to the platform. Handed straight to the adapter.</summary>
    public string? ClientContext { get; init; }
}
