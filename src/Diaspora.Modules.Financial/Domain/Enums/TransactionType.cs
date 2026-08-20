namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>What a transaction was for. The brief's list, in full.</summary>
public enum TransactionType
{
    Payment = 1,
    Withdrawal = 2,
    Refund = 3,
    Credit = 4,
    Bonus = 5,
    Subscription = 6,
    Promotion = 7,
    InternalTransfer = 8,
    Donation = 9,
    CommunityContribution = 10,
    Fee = 11,
    Commission = 12,
    Reversal = 13,
    Adjustment = 14,
    Purchase = 15,
    Payout = 16,

    /// <summary>
    /// A member putting money into their own balance. Distinct from <see cref="Payment"/> because the
    /// money lands somewhere entirely different: a payment credits the platform, a deposit credits
    /// the payer. A single member would have made "what did I spend" unanswerable.
    /// </summary>
    Deposit = 17,

    /// <summary>
    /// Reward points converted into spendable credit. The one path by which something that is not
    /// money becomes money, and it is named so that it can never be mistaken for either a payment or
    /// an operator grant.
    /// </summary>
    RewardConversion = 18
}
