using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// A page of a wallet's statement.
/// <para>
/// Paged by sequence rather than by timestamp. Two entries in one transaction share a timestamp to
/// the microsecond, and a timestamp-only cursor drops one of them silently — which the platform has
/// on record as the worst property a paging bug can have.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetLedgerQuery : IQuery<LedgerPageResponse>
{
    /// <summary>Whose statement. Must belong to the caller unless they hold the staff permission.</summary>
    public required Guid WalletId { get; init; }

    /// <summary>Return entries older than this sequence. Null starts at the newest.</summary>
    public long? AfterSequence { get; init; }

    public int PageSize { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? EntryType { get; init; }
}
