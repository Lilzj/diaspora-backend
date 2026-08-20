namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One human translation of a navigation label. Returned only to curators.</summary>
public sealed record KnowledgeCategoryLocalizationResponse
{
    /// <summary>A normalized BCP-47 tag — <c>fr</c>, <c>fr-MU</c>.</summary>
    public required string Locale { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}
