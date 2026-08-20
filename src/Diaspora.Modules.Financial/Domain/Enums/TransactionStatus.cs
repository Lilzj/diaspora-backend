namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether a transaction's entries are final.</summary>
public enum TransactionStatus
{
    /// <summary>Entries are written and one leg is still awaiting an outcome.</summary>
    Pending = 1,

    Completed = 2,

    Failed = 3,

    /// <summary>
    /// Undone by a compensating transaction. The original entries still exist — reversing a
    /// transaction appends, it never erases.
    /// </summary>
    Reversed = 4
}
