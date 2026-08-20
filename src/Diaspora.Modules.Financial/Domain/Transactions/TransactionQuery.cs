using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Domain.Transactions;

/// <summary>What a transaction-history request asks for.</summary>
public sealed record TransactionQuery
{
    /// <summary>Whose history. Null on a staff query across everybody.</summary>
    public Guid? UserId { get; init; }

    public TransactionType? TransactionType { get; init; }

    public TransactionStatus? Status { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? CountryCode { get; init; }

    public string? Currency { get; init; }

    /// <summary>Return transactions strictly older than this instant.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>
    /// Identifiers already served at the boundary instant.
    /// <para>
    /// ADR-017: exclude by identity and accumulate while the boundary holds. A timestamp-only cursor
    /// silently drops rows tied on the timestamp, and the keyset predicate cannot be written when the
    /// tie-break column is a strongly typed identifier the provider cannot order.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int PageSize { get; init; } = Policies.FinancialLimits.DefaultLedgerPageSize;
}
