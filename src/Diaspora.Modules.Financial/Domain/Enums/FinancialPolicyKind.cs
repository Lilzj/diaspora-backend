namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What a financial policy decides.
/// <para>
/// <b>Every price and every rate on this platform is one of these.</b> Nothing is hard-coded; a
/// commission rate is a policy version with an effective date, which is what makes changing one an
/// operator action with an audit trail rather than a deployment.
/// </para>
/// </summary>
public enum FinancialPolicyKind
{
    /// <summary>What the platform takes from a marketplace sale.</summary>
    MarketplaceCommission = 1,

    HousingCommission = 2,
    ProviderCommission = 3,
    OrganizationCommission = 4,

    /// <summary>The price of a monetization product, when it is not on the product row.</summary>
    ProductPricing = 5,

    SubscriptionPricing = 6,
    PlacementPricing = 7,
    BoostPricing = 8,

    DonationRule = 9,
    ContributionRule = 10,
    PromotionRule = 11,
    RefundRule = 12,

    /// <summary>A flat or proportional platform fee applied to a transaction.</summary>
    PlatformFee = 13,

    /// <summary>Time-boxed pricing for a campaign. The seam behind the client's Promote surface.</summary>
    CampaignPricing = 14,

    /// <summary>How revenue is divided. Declared for a future payout engine; nothing splits today.</summary>
    RevenueDistribution = 15,

    /// <summary>
    /// What a member may add to their own balance: a minimum, a maximum and a daily ceiling.
    /// <para>
    /// A policy rather than a constant because the right answer differs by country, by currency and
    /// by how much regulatory appetite a deployment has — and because a limit somebody has to
    /// redeploy to change is a limit that stays wrong.
    /// </para>
    /// </summary>
    DepositRule = 16,

    /// <summary>What a member may take out: a minimum, a maximum, a daily ceiling and a fee.</summary>
    WithdrawalRule = 17,

    /// <summary>
    /// How a mid-period plan change is priced. Owning this here rather than in a handler is what
    /// stops a client multiplying a price by a remaining fraction and disagreeing with the server at
    /// a rounding boundary.
    /// </summary>
    SubscriptionProration = 18,

    /// <summary>
    /// What reward points are worth, and how many may be converted. The rate lives here so that
    /// nothing else on the platform is ever in a position to price them.
    /// </summary>
    RewardConversion = 19,

    /// <summary>A rule the deployment invented, read by whatever registered it.</summary>
    Custom = 99
}
