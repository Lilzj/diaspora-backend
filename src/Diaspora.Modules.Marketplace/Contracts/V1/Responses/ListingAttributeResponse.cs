namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One structured fact about the item, ready to render.</summary>
public sealed record ListingAttributeResponse
{
    public required string Key { get; init; }

    /// <summary>The label to show. The definition's, or the seller's override, or the key.</summary>
    public required string Label { get; init; }

    public required string Value { get; init; }

    /// <summary>A unit to render beside the value — "km", "GB". From the definition.</summary>
    public string? Unit { get; init; }

    /// <summary>Whether the search surface offers this as a filter. Drives a tappable chip.</summary>
    public required bool IsFilterable { get; init; }
}
