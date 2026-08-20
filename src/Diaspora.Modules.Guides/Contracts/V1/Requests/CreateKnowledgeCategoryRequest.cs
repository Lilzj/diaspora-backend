namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Adds a navigation row to the Knowledge Directory's catalogue.
/// <para>
/// The key is the contract and can never be changed afterwards: it is what a contributor seed
/// declares, what a URL carries and what every country directory entry points at. Everything else
/// about a row is editable.
/// </para>
/// </summary>
public sealed record CreateKnowledgeCategoryRequest
{
    /// <summary>Lower-case kebab. Unique across the catalogue.</summary>
    public required string Key { get; init; }

    /// <summary>The default label, in the platform's own language.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque to the server.</summary>
    public string? Icon { get; init; }

    /// <summary>The catalogue-wide default position. A country may override it.</summary>
    public int Order { get; init; }

    /// <summary>
    /// The guide categories this row draws from. Empty means the guide category whose key matches
    /// this one, which is the common case.
    /// </summary>
    public IReadOnlyList<string> SourceCategoryKeys { get; init; } = [];

    /// <summary>Provider service categories to recommend beside this subject.</summary>
    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];

    public IReadOnlyList<KnowledgeLocalizationInput> Localizations { get; init; } = [];
}
