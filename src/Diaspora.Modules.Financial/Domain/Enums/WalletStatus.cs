namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>What a wallet will currently accept.</summary>
public enum WalletStatus
{
    /// <summary>Created but not yet usable. The state a wallet is born in.</summary>
    Pending = 1,

    /// <summary>Normal. Accepts credits and debits.</summary>
    Active = 2,

    /// <summary>
    /// Accepts credits, refuses debits. What a fraud hold looks like: money can still arrive and be
    /// refunded, and nothing can leave.
    /// </summary>
    Frozen = 3,

    /// <summary>Refuses everything. Balances are preserved; the ledger is never rewritten.</summary>
    Closed = 4
}
