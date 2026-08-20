namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Which way an entry moved the balance, from the wallet owner's point of view.
/// <para>
/// Deliberately the everyday statement sense rather than the double-entry bookkeeping one: a credit
/// increases what the owner has. The brief's <c>Lifetime Credits</c> and <c>Lifetime Debits</c> are
/// the sums of these two, and a member reading their statement must not have to know which side of
/// the platform's books they are on.
/// </para>
/// </summary>
public enum LedgerDirection
{
    /// <summary>Decreases the balance.</summary>
    Debit = 1,

    /// <summary>Increases the balance.</summary>
    Credit = 2
}
