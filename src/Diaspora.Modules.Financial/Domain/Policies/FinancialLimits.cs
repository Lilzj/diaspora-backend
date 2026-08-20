namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>
/// The bounds the domain enforces, in one place.
/// <para>
/// Declared here rather than inline so a limit means the same thing in the aggregate that enforces
/// it, the validator that reports it and the column that stores it. A limit spelled two ways is a
/// request that passes validation and fails at the database.
/// </para>
/// </summary>
public static class FinancialLimits
{
    /// <summary>
    /// The largest single amount, in minor units. Ten billion major units at two decimals — far
    /// above any legitimate transaction and far below <see cref="long.MaxValue"/>, so summing a
    /// lifetime of them cannot overflow.
    /// </summary>
    public const long MaxAmountMinorUnits = 1_000_000_000_000L;

    public const int CurrencyCodeLength = 3;

    public const int MaxCodeLength = 64;

    public const int MaxNameLength = 160;

    public const int MaxDescriptionLength = 1024;

    public const int MaxReasonLength = 512;

    public const int MaxReferenceLength = 256;

    /// <summary>The longest a placement may be bought for in one purchase. Two years.</summary>
    public const int MaxPlacementDays = 730;

    /// <summary>The longest a payment intent stays open before it expires. Seven days.</summary>
    public const int PaymentIntentLifetimeHours = 168;

    /// <summary>How long a failed subscription payment keeps access. Fourteen days.</summary>
    public const int SubscriptionGracePeriodDays = 14;

    /// <summary>The most entries one statement page may return.</summary>
    public const int MaxLedgerPageSize = 100;

    public const int DefaultLedgerPageSize = 25;

    /// <summary>The most promotions one purchase may stack. One.</summary>
    public const int MaxPromotionsPerPurchase = 1;

    /// <summary>The largest commission rate a policy may express, as a percentage.</summary>
    public const decimal MaxCommissionPercentage = 50m;
}
