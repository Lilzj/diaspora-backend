using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>One subscription plan a module wants in the catalogue at startup.</summary>
public sealed record SubscriptionPlanSeed
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required SubscriptionTier Tier { get; init; }

    public required BillingInterval Interval { get; init; }

    /// <summary>Major units. Zero for a free plan.</summary>
    public required decimal PriceAmount { get; init; }

    public required string Currency { get; init; }

    public int TrialDays { get; init; }

    public FinancialAccountType AudienceType { get; init; } = FinancialAccountType.Personal;

    public string? CountryCode { get; init; }

    public int SortOrder { get; init; } = 100;

    public bool IsSystem { get; init; }

    /// <summary>
    /// What the plan grants, as capability keys with optional limits. The client renders these and
    /// never the plan name, which is what lets a plan be added with no client release.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Entitlements { get; init; }
}
