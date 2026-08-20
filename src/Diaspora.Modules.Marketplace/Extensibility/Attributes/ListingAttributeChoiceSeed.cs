namespace Diaspora.Modules.Marketplace.Extensibility.Attributes;

/// <summary>One option on a contributed choice attribute.</summary>
public sealed record ListingAttributeChoiceSeed
{
    /// <summary>The stored form. Stable, and what a filter matches.</summary>
    public required string Value { get; init; }

    /// <summary>What the client renders. Free to change without rewriting any listing.</summary>
    public string? Label { get; init; }
}
