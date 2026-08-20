namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What a purchased placement buys.
/// <para>
/// The first four names are taken verbatim from <c>ListingFeaturedIntegrationEvent.PromotionKind</c>,
/// which Marketplace has published since B10. Minting a fifth spelling of "featured" would leave two
/// vocabularies for one idea and a consumer switching on the wrong one.
/// </para>
/// </summary>
public enum PlacementKind
{
    /// <summary>Appears in a featured rail.</summary>
    Featured = 1,

    /// <summary>Moved to the top of the ordinary list. The paid counterpart of a free renew.</summary>
    Bump = 2,

    Spotlight = 3,
    CategoryHighlight = 4,

    /// <summary>
    /// Raised ranking weight and reach rather than a rail. What the client's Boost action buys:
    /// "increase search priority and local reach".
    /// </summary>
    Boost = 5,

    /// <summary>The whole premium treatment for a subject, for a period.</summary>
    Premium = 6
}
