namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Starts a translation of an existing guide: a new draft in the target language, linked
/// through the source's translation group and seeded with the source's structure.
/// </summary>
public sealed record CreateGuideTranslationRequest
{
    /// <summary>The target language. Lower-case ISO 639-1.</summary>
    public required string Language { get; init; }

    /// <summary>The translated title. Omitted, the source title carries over untranslated.</summary>
    public string? Title { get; init; }

    /// <summary>A requested handle for the translation. Omitted, one is derived.</summary>
    public string? Slug { get; init; }
}
