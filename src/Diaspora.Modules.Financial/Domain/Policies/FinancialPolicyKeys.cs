namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>
/// The policy keys the platform ships, in one place.
/// <para>
/// A key is the stable identity of a rule across every version of it, and it appears in three
/// places: the seeded catalogue, the engine that resolves it, and the audit stamp on every ledger
/// entry the rule priced. A key spelled two ways is a policy that silently stops resolving and a
/// commission that silently becomes a refusal.
/// </para>
/// <para>
/// A deployment may add keys freely; nothing here is exhaustive and nothing in the engine switches
/// on one.
/// </para>
/// </summary>
public static class FinancialPolicyKeys
{
    public const string MarketplaceCommission = "marketplace.commission";
    public const string HousingCommission = "housing.commission";
    public const string ProviderCommission = "provider.commission";
    public const string OrganizationCommission = "organization.commission";

    public const string PlatformFee = "platform.fee";
    public const string DonationFee = "donation.fee";
    public const string ContributionFee = "contribution.fee";

    public const string PlacementPricing = "placement.price";
    public const string BoostPricing = "boost.price";

    /// <summary>How long after a purchase a placement may be cancelled with a refund.</summary>
    public const string RefundWindow = "refund.window";

    /// <summary>How a sale divides between the platform and the seller.</summary>
    public const string RevenueDistribution = "revenue.distribution";

    /// <summary>What a member may add to their own balance, and how often.</summary>
    public const string DepositLimits = "deposit.limits";

    /// <summary>What a member may take out, how often, and what it costs them.</summary>
    public const string WithdrawalLimits = "withdrawal.limits";

    /// <summary>How a mid-period plan change is priced.</summary>
    public const string SubscriptionProration = "subscription.proration";

    /// <summary>What a reward point is worth, and how many may be converted.</summary>
    public const string RewardConversion = "reward.conversion";
}

/// <summary>
/// The parameter names the shipped policies use.
/// <para>
/// A policy's parameters are a loose bag of name/value pairs on purpose — that is what lets a
/// deployment add a rule this module has never heard of. The ones B15 itself reads are named here so
/// that the seeder writing a parameter and the handler looking one up cannot disagree about its
/// spelling, which is a limit that silently stops applying.
/// </para>
/// </summary>
public static class FinancialPolicyParameters
{
    public const string MinimumMinorUnits = "minimumMinorUnits";
    public const string MaximumMinorUnits = "maximumMinorUnits";
    public const string DailyMaximumMinorUnits = "dailyMaximumMinorUnits";
    public const string DailyCount = "dailyCount";
    public const string FeeMinorUnits = "feeMinorUnits";
    public const string FeePercentage = "feePercentage";

    /// <summary>Minor units of credit one reward point converts into.</summary>
    public const string MinorUnitsPerPoint = "minorUnitsPerPoint";

    /// <summary>
    /// The ISO 4217 currency a <see cref="MinorUnitsPerPoint"/> rate is in. Required for the rate to
    /// apply: a count of minor units with no currency is a pound or a kobo depending on who asks.
    /// </summary>
    public const string Currency = "currency";

    public const string MinimumPoints = "minimumPoints";
    public const string MaximumPoints = "maximumPoints";
    public const string DailyMaximumPoints = "dailyMaximumPoints";

    /// <summary>Days a member has to ask for a refund after a payment captured.</summary>
    public const string WindowDays = "windowDays";

    /// <summary>Whether an upgrade charges the difference immediately.</summary>
    public const string ProrateUpgrade = "prorateUpgrade";

    /// <summary>Whether a downgrade takes effect at the end of the paid period.</summary>
    public const string DeferDowngrade = "deferDowngrade";

    /// <summary>How many days before a window closes the platform announces it.</summary>
    public const string ExpiryLeadDays = "expiryLeadDays";
}
