namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Why credit was granted.</summary>
public enum CreditKind
{
    Promotional = 1,
    Compensation = 2,
    Referral = 3,

    /// <summary>Returned as credit rather than to the original instrument.</summary>
    RefundToCredit = 4,

    Bonus = 5,

    /// <summary>Bought outright. A credit bundle.</summary>
    Purchased = 6
}
