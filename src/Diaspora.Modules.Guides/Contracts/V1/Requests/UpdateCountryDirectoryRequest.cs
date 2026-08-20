namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Rewrites a country directory's header copy. The country code never changes — it is the identity.
/// </summary>
public sealed record UpdateCountryDirectoryRequest
{
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    public string? PrimaryLanguage { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Replacement, not a patch. Omitting it clears the translations.</summary>
    public IReadOnlyList<KnowledgeLocalizationInput> Localizations { get; init; } = [];

    /// <summary>The revision the client was looking at. See the category update for why.</summary>
    public int? Revision { get; init; }
}
