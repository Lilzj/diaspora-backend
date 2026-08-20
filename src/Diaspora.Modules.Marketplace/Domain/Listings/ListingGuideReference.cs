using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// A guide shown beside the listing — how to buy a used car safely, what an import duty costs,
/// how to check an electronics serial number.
/// <para>
/// <b>This is the contextual link that makes the two modules worth having together.</b> A
/// second-hand vehicle listed in a country the buyer moved to last year is exactly the moment
/// B8's registration guide is worth reading, and putting it on the listing means they get it
/// without knowing the knowledge base exists.
/// </para>
/// <para>
/// <b><see cref="IsAutomatic"/> separates the platform's suggestions from the seller's own
/// choices</b>, so refreshing suggestions when a listing changes country or category never
/// discards a link somebody deliberately added.
/// </para>
/// </summary>
public sealed class ListingGuideReference
{
    private ListingGuideReference(Guid guideId, string? label, bool isAutomatic, int order)
    {
        GuideId = guideId;
        Label = label;
        IsAutomatic = isAutomatic;
        Order = order;
    }

    private ListingGuideReference() { }

    /// <summary>B8's guide identifier. The title is B8's answer at read time.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>An override for the link text. Null renders B8's own title.</summary>
    public string? Label { get; private set; }

    /// <summary>Whether the platform suggested it. Seller choices are never auto-replaced.</summary>
    public bool IsAutomatic { get; private set; }

    public int Order { get; private set; }

    public static ListingGuideReference Create(
        Guid guideId, string? label, bool isAutomatic, int order)
    {
        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxDisplayName })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxDisplayName];
        }

        return new ListingGuideReference(guideId, trimmed, isAutomatic, order);
    }
}
