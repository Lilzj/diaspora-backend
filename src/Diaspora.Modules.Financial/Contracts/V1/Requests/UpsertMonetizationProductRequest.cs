namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Add or change a catalogue entry. Staff only.
/// <para>
/// This is how a premium feature is created on this platform. There is no other way, and there is no
/// code path anywhere that hard-codes one.
/// </para>
/// </summary>
public sealed record UpsertMonetizationProductRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string SubjectType { get; init; }

    /// <summary>Major units. Omit when a pricing policy decides instead.</summary>
    public decimal? PriceAmount { get; init; }

    public string? Currency { get; init; }

    /// <summary>A policy key that answers the price. Use for country or campaign pricing.</summary>
    public string? PricingPolicyKey { get; init; }

    public int? DurationDays { get; init; }

    /// <summary>
    /// <c>Featured</c>, <c>Bump</c>, <c>Spotlight</c>, <c>CategoryHighlight</c>, <c>Boost</c> or
    /// <c>Premium</c>. Omit for a product that grants no placement.
    /// </summary>
    public string? PlacementKind { get; init; }

    public decimal? PriorityWeight { get; init; }

    public bool IsRenewable { get; init; } = true;

    public int SortOrder { get; init; } = 100;

    /// <summary>Eligibility conditions as kind-to-value pairs. Replaces whatever was set.</summary>
    public IReadOnlyDictionary<string, string>? Eligibility { get; init; }

    /// <summary>Whether the product is on sale once saved.</summary>
    public bool Activate { get; init; } = true;

    public DateTimeOffset? AvailableFrom { get; init; }

    public DateTimeOffset? AvailableTo { get; init; }
}
