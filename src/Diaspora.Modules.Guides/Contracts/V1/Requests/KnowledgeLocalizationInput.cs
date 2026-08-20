namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One translation, on the way in. Serves both the category label and the directory header —
/// they carry the same three fields, and a second near-identical record would be two shapes a
/// curation client has to know about instead of one.
/// </summary>
public sealed record KnowledgeLocalizationInput
{
    /// <summary>A BCP-47 tag. Case is normalized server-side; <c>fr-mu</c> and <c>fr-MU</c> are one.</summary>
    public required string Locale { get; init; }

    /// <summary>The localized name, or display name for a directory header.</summary>
    public required string Name { get; init; }

    /// <summary>The localized description, or summary for a directory header.</summary>
    public string? Description { get; init; }
}
