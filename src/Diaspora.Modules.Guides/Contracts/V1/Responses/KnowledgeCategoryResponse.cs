namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One navigation row of the Knowledge Directory.
/// <para>
/// <see cref="Name"/> and <see cref="Description"/> are already resolved for the reader's locale —
/// the client never has to pick a translation, because picking one correctly means knowing the
/// language-only fallback rule, and a rule implemented on both sides is a rule implemented
/// differently on both sides. <see cref="Localizations"/> is populated only on the curator surface,
/// where editing them is the point.
/// </para>
/// </summary>
public sealed record KnowledgeCategoryResponse
{
    public required Guid KnowledgeCategoryId { get; init; }

    /// <summary>The stable key. What a URL carries and a client switches on.</summary>
    public required string Key { get; init; }

    /// <summary>Already localized for the caller.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An emoji or icon name. Opaque — the client decides what it is.</summary>
    public string? Icon { get; init; }

    public required int Order { get; init; }

    /// <summary>Whether the row may still be placed on a country directory.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The guide categories this row draws from, already expanded.</summary>
    public IReadOnlyList<string> SourceCategoryKeys { get; init; } = [];

    /// <summary>Provider service categories to offer beside this subject. Keys only.</summary>
    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];

    /// <summary>The optimistic concurrency token. Sent back on an update.</summary>
    public required int Revision { get; init; }

    /// <summary>Populated on the curator surface only.</summary>
    public IReadOnlyList<KnowledgeCategoryLocalizationResponse> Localizations { get; init; } = [];
}
