namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What kind of thing a catalogue entry sells.
/// <para>
/// Every category the brief names, and no category it does not. A deployment adding a product picks
/// one of these; nothing in this module switches on the value, which is what keeps the catalogue
/// data rather than code.
/// </para>
/// </summary>
public enum MonetizationProductCategory
{
    PremiumListing = 1,
    FeaturedListing = 2,
    BoostedListing = 3,
    PremiumProvider = 4,
    FeaturedProvider = 5,
    PremiumOrganization = 6,
    FeaturedOrganization = 7,
    FeaturedEvent = 8,
    BoostedJob = 9,
    HousingHighlight = 10,
    CreatorPromotion = 11,
    DonationPackage = 12,
    ContributionPackage = 13,
    SubscriptionPlan = 14,
    CreditBundle = 15,

    /// <summary>Sponsored placement. The seam behind the client's "Promote (Coming Soon)" action.</summary>
    Campaign = 16
}
