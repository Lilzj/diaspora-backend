namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// One prerequisite a listing must satisfy before it can be published.
/// <para>
/// The set exists so a refusal can say which prerequisites are unmet rather than reciting all
/// of them. The bar itself is unchanged and lives in
/// <see cref="MarketplaceListing.UnmetPublishRequirements"/>; this is only the vocabulary for
/// describing it.
/// </para>
/// </summary>
public enum ListingPublishRequirement
{
    Title,
    Description,
    Category,
    Country,

    /// <summary>A price, where the listing type requires one. A wanted advert does not.</summary>
    Price,

    /// <summary>Some way of getting the item to a buyer — pickup, delivery, shipping or digital.</summary>
    Fulfillment,

    /// <summary>
    /// At least one cover or gallery image. Not a stylistic preference: a listing with no
    /// image is one nobody opens and nobody can sanity-check against its description, which
    /// makes it the cheapest fraud control the module has.
    /// </summary>
    Photograph,
}

/// <summary>
/// Seller-facing wording for each prerequisite, kept beside the enum so a new requirement
/// cannot be added without one.
/// </summary>
public static class ListingPublishRequirementText
{
    public static string Describe(ListingPublishRequirement requirement) => requirement switch
    {
        ListingPublishRequirement.Title => "a title",
        ListingPublishRequirement.Description => "a description",
        ListingPublishRequirement.Category => "a category",
        ListingPublishRequirement.Country => "a country",
        ListingPublishRequirement.Price => "a price",
        ListingPublishRequirement.Fulfillment => "a delivery option — pickup, delivery, shipping or digital",
        ListingPublishRequirement.Photograph => "at least one photograph",
        _ => requirement.ToString(),
    };

    /// <summary>The machine-readable token a client can branch on.</summary>
    public static string Token(ListingPublishRequirement requirement) => requirement switch
    {
        ListingPublishRequirement.Title => "title",
        ListingPublishRequirement.Description => "description",
        ListingPublishRequirement.Category => "category",
        ListingPublishRequirement.Country => "country",
        ListingPublishRequirement.Price => "price",
        ListingPublishRequirement.Fulfillment => "fulfillment",
        ListingPublishRequirement.Photograph => "photograph",
        _ => requirement.ToString().ToLowerInvariant(),
    };
}
