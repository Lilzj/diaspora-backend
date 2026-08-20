namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Which of a wallet's three balances an entry moves.
/// <para>
/// A hold is not a special kind of entry — it is a debit of <see cref="Available"/> and a credit of
/// <see cref="Reserved"/> inside one transaction, which is why the two always agree and why
/// releasing one is the same operation reversed rather than a second mechanism.
/// </para>
/// </summary>
public enum LedgerBucket
{
    /// <summary>Spendable now.</summary>
    Available = 1,

    /// <summary>Arriving but not yet settled. An authorised, uncaptured payment sits here.</summary>
    Pending = 2,

    /// <summary>Committed against something. Held by an intent, a purchase or an escrow.</summary>
    Reserved = 3
}
