namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>How often a subscription renews.</summary>
public enum BillingInterval
{
    Monthly = 1,
    Quarterly = 2,
    Yearly = 3,

    /// <summary>Charged once, never renews. Still a subscription because it grants entitlements.</summary>
    Lifetime = 4
}
