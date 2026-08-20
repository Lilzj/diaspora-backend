namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One option on a choice attribute.</summary>
public sealed record AttributeChoiceResponse
{
    /// <summary>The stored form. What a listing keeps and what a filter matches.</summary>
    public required string Value { get; init; }

    /// <summary>What to render. Free to change without rewriting any listing.</summary>
    public required string Label { get; init; }
}
