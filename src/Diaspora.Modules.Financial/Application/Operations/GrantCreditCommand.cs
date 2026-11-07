using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Grants platform credit.
/// <para>
/// <b>Transactional: it writes the grant and posts the ledger entries that give it a balance.</b> A
/// grant with no entries behind it is a promise the wallet cannot see.
/// </para>
/// <para>
/// <b>Bounded by <see cref="CreditGrantPolicy"/> (7 Nov 2026):</b> not to yourself, a reason, at most
/// 100 of the currency, and above 50 a second Admin through <see cref="RequestCreditGrantCommand"/>.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.CreditManage)]
[Transactional(Reason = "A grant writes the grant record and posts the ledger entries behind it.")]
[Auditable(Action = "financial.credit.grant")]
public sealed record GrantCreditCommand : ICommand<CreditGrantResponse>
{
    public required Guid UserId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Kind { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}
