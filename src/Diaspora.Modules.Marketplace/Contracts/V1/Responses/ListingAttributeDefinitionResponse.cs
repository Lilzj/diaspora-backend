namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// What a category expects a seller to state.
/// <para>
/// This is what the create form is generated from: the client asks for a category's definitions
/// and renders a field per definition, of the kind the definition names. That is the whole reason
/// attributes are data rather than columns — a new category ships with its form already built.
/// </para>
/// </summary>
public sealed record ListingAttributeDefinitionResponse
{
    public required Guid DefinitionId { get; init; }

    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>The subtree it applies to — <c>/vehicles/</c>.</summary>
    public required string CategoryPath { get; init; }

    /// <summary><c>Text</c>, <c>Number</c>, <c>Boolean</c> or <c>Choice</c>.</summary>
    public required string ValueKind { get; init; }

    /// <summary>Whether a listing must state it before publishing.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the search surface offers it as a filter.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>A unit to render beside the input — "km", "GB".</summary>
    public string? Unit { get; init; }

    public required int Order { get; init; }

    /// <summary>The options, when the kind is <c>Choice</c>. Empty otherwise.</summary>
    public required IReadOnlyList<AttributeChoiceResponse> Choices { get; init; }
}
