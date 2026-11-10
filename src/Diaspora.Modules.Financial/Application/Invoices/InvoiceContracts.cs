using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Invoices;

/// <summary>
/// Numbers every captured payment of the caller's that the platform kept and has not invoiced yet.
/// <para>
/// <b>Run by the list route before it reads, and moves no money.</b> Issuing at read time rather than
/// inside the capture keeps the capture's transaction free of a numbering race: a capture that failed
/// because two invoices wanted the same number would be the wrong thing to lose. The payment is the
/// fact; the invoice is a document about it, and is produced the first time anybody asks.
/// </para>
/// <para>
/// Not <c>[Transactional]</c>: each invoice saves on its own, so a number lost to a concurrent issuer
/// is retried for that one payment without rolling back the others.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record IssueMyInvoicesCommand : ICommand<int>;

/// <summary>The caller's invoices, newest number first.</summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetMyInvoicesQuery : IQuery<IReadOnlyList<InvoiceResponse>>
{
    /// <summary>An invoice number already served. The page starts strictly after it.</summary>
    public string? Before { get; init; }

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>One invoice. Only its owner may read it; anybody else is told it does not exist.</summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetInvoiceQuery : IQuery<InvoiceResponse>
{
    public required Guid InvoiceId { get; init; }
}

/// <summary>One invoice as a printable HTML page. Owner only, exactly as <see cref="GetInvoiceQuery"/>.</summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetInvoiceDocumentQuery : IQuery<string>
{
    public required Guid InvoiceId { get; init; }
}
