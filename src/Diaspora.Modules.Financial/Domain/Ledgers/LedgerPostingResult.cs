using Diaspora.Modules.Financial.Domain.Transactions;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>What a posting produced: the transaction and the entries it appended.</summary>
public sealed record LedgerPostingResult
{
    public required FinancialTransaction Transaction { get; init; }

    public required IReadOnlyList<LedgerEntry> Entries { get; init; }
}
