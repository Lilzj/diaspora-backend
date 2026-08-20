namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>One structured fact about an item, as the client sends it.</summary>
public sealed record ListingAttributeRequest
{
    /// <summary>The definition's key. Normalised server-side, so casing and spacing are free.</summary>
    public required string Key { get; init; }

    public required string Value { get; init; }

    /// <summary>An override for the label. Null renders the definition's own.</summary>
    public string? DisplayName { get; init; }
}
