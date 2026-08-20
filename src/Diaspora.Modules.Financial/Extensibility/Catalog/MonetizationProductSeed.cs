using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>One monetization product a module wants in the catalogue at startup.</summary>
public sealed record MonetizationProductSeed
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required MonetizationProductCategory Category { get; init; }

    public required MonetizationSubjectType SubjectType { get; init; }

    /// <summary>Major units. Null when <see cref="PricingPolicyKey"/> answers instead.</summary>
    public decimal? PriceAmount { get; init; }

    public string? Currency { get; init; }

    public string? PricingPolicyKey { get; init; }

    public int? DurationDays { get; init; }

    public PlacementKind? PlacementKind { get; init; }

    /// <summary>What a ranking consumer should weight the subject by. 1.0 is no change.</summary>
    public decimal PriorityWeight { get; init; } = 1m;

    public bool IsRenewable { get; init; } = true;

    public int SortOrder { get; init; } = 100;

    public bool IsSystem { get; init; }

    /// <summary>Eligibility conditions, as kind-to-value pairs.</summary>
    public IReadOnlyDictionary<EligibilityRuleKind, string> Eligibility { get; init; } =
        new Dictionary<EligibilityRuleKind, string>();
}
