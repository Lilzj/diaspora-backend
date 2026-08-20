namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>What a promotion is.</summary>
public enum PromotionKind
{
    /// <summary>A code somebody types.</summary>
    Coupon = 1,

    /// <summary>Applied automatically while a campaign runs. No code.</summary>
    CampaignDiscount = 2,

    ReferralReward = 3,

    /// <summary>Grants wallet credit rather than reducing a price.</summary>
    CreditBonus = 4,

    /// <summary>Starts or extends a subscription trial.</summary>
    FreeTrial = 5
}
