namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One row of the curated shelf, as a curator sees it.
/// <para>
/// Carries the guide card as well as the placement, because a curator reviewing a shelf needs to
/// see what they placed, not a list of identifiers. The card is resolved at read time from B8 and
/// is null for a guide that has since been unpublished — which is exactly the row a curator most
/// needs to see, because it is the one to remove.
/// </para>
/// </summary>
public sealed record CountryFeaturedGuideResponse
{
    public required Guid GuideId { get; init; }

    /// <summary>The category it was featured within, or null for the country's own shelf.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    public required int Position { get; init; }

    /// <summary>The curator's one-line reason.</summary>
    public string? Note { get; init; }

    public required DateTimeOffset FeaturedAt { get; init; }

    /// <summary>Null when the guide is no longer published — the row a curator should remove.</summary>
    public GuideCardResponse? Guide { get; init; }
}
