namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Why an entry exists.
/// <para>
/// The brief's list, plus the six operations the platform cannot express without one: a payment, a
/// fee, a reversal, an internal transfer, a purchase and a payout. <see cref="Debit"/> and
/// <see cref="Credit"/> are the generic movements for a reason none of the others names — they are
/// not the direction, which is <see cref="LedgerDirection"/>.
/// </para>
/// </summary>
public enum LedgerEntryType
{
    Debit = 1,
    Credit = 2,
    Hold = 3,
    Release = 4,
    Refund = 5,
    Adjustment = 6,
    Commission = 7,
    Bonus = 8,
    Promotion = 9,
    Subscription = 10,
    Donation = 11,
    CommunityContribution = 12,
    Fee = 13,
    Reversal = 14,
    InternalTransfer = 15,
    Purchase = 16,
    Payout = 17,
    Payment = 18,

    /// <summary>Money a member added to their own balance from outside the platform.</summary>
    Deposit = 19,

    /// <summary>Credit produced by converting reward points. Never a payment and never a grant.</summary>
    RewardConversion = 20
}
