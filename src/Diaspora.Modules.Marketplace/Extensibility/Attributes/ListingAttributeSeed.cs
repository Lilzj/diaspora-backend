using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Extensibility.Attributes;

/// <summary>
/// One category attribute as a contributor declares it.
/// <para>
/// Names domain enums, and that is a deliberate exception to the rule that the extensibility
/// surface stays primitive: <see cref="AttributeValueKind"/> is the vocabulary the declaration is
/// expressed in, a contributor switches on it, and flattening it to a string here would only move
/// the parsing into every contributor.
/// </para>
/// </summary>
public sealed record ListingAttributeSeed
{
    /// <summary>Lower-case, hyphenated. What a listing attribute and a filter both name.</summary>
    public required string Key { get; init; }

    /// <summary>What the form labels the field.</summary>
    public required string Label { get; init; }

    /// <summary>
    /// The category subtree it applies to — <c>vehicles</c>, <c>/electronics/mobile-phones/</c>,
    /// or null for the whole marketplace. Leading and trailing slashes are optional.
    /// </summary>
    public string? CategoryPath { get; init; }

    public AttributeValueKind ValueKind { get; init; } = AttributeValueKind.Text;

    /// <summary>Whether a listing in this subtree must state it before publishing.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Whether the search surface offers it as a filter.</summary>
    public bool IsFilterable { get; init; } = true;

    /// <summary>A unit to render beside the value — "km", "GB". Never parsed.</summary>
    public string? Unit { get; init; }

    public int Order { get; init; }

    /// <summary>
    /// The options, for a choice attribute. Each is a stored value and the label to render for it;
    /// the value is what listings keep, so translating the label never rewrites existing listings.
    /// </summary>
    public IReadOnlyList<ListingAttributeChoiceSeed>? Choices { get; init; }
}
