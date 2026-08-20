namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Where a purchase of a monetization product got to.</summary>
public enum PurchaseStatus
{
    /// <summary>Recorded, awaiting payment.</summary>
    Pending = 1,

    /// <summary>Paid. Whatever the product grants has been granted.</summary>
    Completed = 2,

    Failed = 3,

    /// <summary>Paid, then returned. What the product granted has been withdrawn.</summary>
    Refunded = 4,

    /// <summary>Abandoned before payment.</summary>
    Cancelled = 5
}
