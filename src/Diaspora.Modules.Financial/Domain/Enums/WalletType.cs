namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What a wallet is for.
/// <para>
/// One financial account may hold several: a primary balance, an escrow pot holding money that is
/// promised but not earned, a revenue pot, and a promotional pot whose credit expires. Keeping them
/// as separate wallets rather than separate columns means every one of them is derived from the
/// same ledger by the same rule.
/// </para>
/// </summary>
public enum WalletType
{
    /// <summary>The everyday balance. Every account has exactly one per currency.</summary>
    Primary = 1,

    /// <summary>
    /// Money held on behalf of somebody else pending an outcome. The seam a future escrow engine
    /// lands on; nothing releases from one today.
    /// </summary>
    Escrow = 2,

    /// <summary>Platform earnings. Commission and fees are credited here.</summary>
    Revenue = 3,

    /// <summary>Granted credit rather than deposited money. Can expire; cannot be withdrawn.</summary>
    Promotional = 4,

    /// <summary>
    /// The boundary with the outside world, and the reason every transaction on this platform
    /// balances.
    /// <para>
    /// A card capture credits a member and debits clearing; a payout debits a member and credits
    /// clearing. Without it, money arriving from a gateway would be a credit with no matching debit
    /// — a ledger that does not balance, which is a ledger that cannot be checked. Only the platform
    /// account holds one.
    /// </para>
    /// </summary>
    Clearing = 5,

    /// <summary>
    /// The platform's rewards pot, one per currency: what converted reward points are paid from.
    /// <para>
    /// <b>Funded, and bounded by its funding.</b> Conversions used to debit Clearing, the one wallet
    /// allowed to go negative, so the promise to members had no ceiling and sat inside the deposit
    /// float. This wallet cannot go negative: staff top it up from Revenue each period, and when it
    /// cannot cover a conversion, conversion pauses (D-6). Only the platform account holds one.
    /// </para>
    /// </summary>
    Rewards = 6
}
