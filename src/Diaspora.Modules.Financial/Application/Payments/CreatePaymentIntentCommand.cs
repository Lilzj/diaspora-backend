using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Starts a payment.
/// <para>
/// <b>Transactional, because a first payment provisions the payer's financial identity as well as
/// the intent.</b> Two writes that must land together: an intent whose account was never saved would
/// reference a row that does not exist.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "A first payment opens the payer's financial identity alongside the intent.")]
[Auditable(Action = "financial.payment.create")]
public sealed record CreatePaymentIntentCommand : ICommand<PaymentIntentResponse>
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string MethodKind { get; init; }

    public required string Description { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}
