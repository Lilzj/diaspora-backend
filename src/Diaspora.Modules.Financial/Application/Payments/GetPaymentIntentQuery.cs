using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>One payment, with its refunds.</summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetPaymentIntentQuery : IQuery<PaymentIntentResponse>
{
    public required Guid PaymentIntentId { get; init; }
}
