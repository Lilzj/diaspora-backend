namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One row of the home feed.
/// <para>
/// <b>Shaped as a section rather than a flat list because the client already renders sections.</b>
/// The app draws "Trending Guides" as a horizontal carousel and "Community Updates" as a vertical
/// list; a backend that returned twenty undifferentiated cards would leave the client to invent the
/// grouping, and iOS and Android would invent it differently.
/// </para>
/// <para>
/// <see cref="Key"/> is what the client switches its layout on and is stable across releases.
/// <see cref="SeeAllRoute"/> is the client route the row's "see all" affordance leads to, supplied by
/// the module that contributed the section — so a vertical adding a home-feed row does not need a
/// client change to make it navigable.
/// </para>
/// </summary>
public sealed record FeedSectionResponse
{
    /// <summary>The stable layout key — <c>for-you</c>, <c>trending-guides</c>, and so on.</summary>
    public required string Key { get; init; }

    /// <summary>English. The client prefers <see cref="TitleResourceKey"/>.</summary>
    public required string Title { get; init; }

    public required string TitleResourceKey { get; init; }

    /// <summary>Null on a mixed row. What the client uses to pick a card component.</summary>
    public string? TargetType { get; init; }

    public required IReadOnlyList<RecommendationResponse> Items { get; init; }

    /// <summary>The client route behind "see all".</summary>
    public string? SeeAllRoute { get; init; }

    /// <summary>True when this row's items came from the cold-start path.</summary>
    public required bool IsColdStart { get; init; }
}
