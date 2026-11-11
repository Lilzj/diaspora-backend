using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Asks for a credit grant that needs a second Admin (7 Nov 2026). Grants nothing.
/// <para>
/// Bounded like a grant: not to yourself, a reason, and no more than
/// <see cref="CreditGrantPolicy.MaximumMajorUnits"/>. Any amount up to that may be asked for, so an
/// Admin who wants a second pair of eyes on a smaller grant can have them.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.CreditManage)]
[Transactional(Reason = "A credit grant request is one record.")]
[Auditable(Action = "financial.credit.grant_request")]
public sealed record RequestCreditGrantCommand : ICommand<CreditGrantRequestResponse>
{
    public required Guid UserId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Kind { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Approves, rejects or withdraws a credit grant request (7 Nov 2026).
/// <para>
/// <b>Approval needs a different Admin from the one who asked, and not the member it is for,</b> and
/// issues the credit in the same transaction, naming both. The Admin who asked may decline their own
/// request, which withdraws it: on a deployment with one Admin nobody else could close it.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.CreditManage)]
[Transactional(Reason = "Approving a request and issuing its credit are one change.")]
[Auditable(Action = "financial.credit.grant_decide")]
public sealed record DecideCreditGrantRequestCommand : ICommand<CreditGrantRequestResponse>
{
    public required Guid RequestId { get; init; }

    public required bool Approve { get; init; }

    /// <summary>Why it was declined. Required to decline; optional on an approval.</summary>
    public string? Note { get; init; }
}

/// <summary>Credit grant requests, newest first; pending by default. Staff.</summary>
[RequirePermission(FinancialPermissions.WalletReadAny)]
public sealed record GetCreditGrantRequestsQuery : IQuery<IReadOnlyList<CreditGrantRequestResponse>>
{
    /// <summary><c>Pending</c> (the default), <c>Approved</c>, <c>Rejected</c>, <c>Withdrawn</c>, or <c>all</c>.</summary>
    public string? State { get; init; }

    /// <summary>The instant the previous page ended on. Inclusive — see <see cref="ExcludeIds"/>.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>The requests already shown at <see cref="Before"/>.</summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
