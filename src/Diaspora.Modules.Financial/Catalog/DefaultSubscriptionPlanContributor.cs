using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Catalog;

namespace Diaspora.Modules.Financial.Catalog;

/// <summary>
/// The plans the platform ships.
/// <para>
/// <b>The free plan is a real plan and not the absence of one.</b> "No subscription" and "the free
/// plan" being the same answer means every caller reads entitlements from one place; the alternative
/// is every surface having a null check and its own idea of what somebody with no plan may do.
/// </para>
/// <para>
/// <b>Entitlements are what the client renders, never plan names.</b> A screen switching on
/// <c>planCode == "professional"</c> would have to ship again the day somebody inserts a plan
/// between two existing ones; a screen rendering <c>listings.concurrent</c> and
/// <c>analytics.advanced</c> never does.
/// </para>
/// </summary>
public sealed class DefaultSubscriptionPlanContributor : ISubscriptionPlanContributor
{
    private const string Gbp = "GBP";

    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<SubscriptionPlanSeed> GetPlans()
    {
        yield return new SubscriptionPlanSeed
        {
            Code = "free",
            Name = "Free",
            Description = "Everything the community runs on. No card, no expiry.",
            Tier = SubscriptionTier.Free,
            Interval = BillingInterval.Lifetime,
            PriceAmount = 0m,
            Currency = Gbp,
            SortOrder = 10,
            IsSystem = true,
            Entitlements = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["listings.concurrent"] = "5",
                ["guides.publish"] = null,
                ["messaging.basic"] = null
            }
        };

        yield return new SubscriptionPlanSeed
        {
            Code = "premium.monthly",
            Name = "Premium",
            Description = "More listings, richer analytics and a monthly boost allowance.",
            Tier = SubscriptionTier.Premium,
            Interval = BillingInterval.Monthly,
            PriceAmount = 7.99m,
            Currency = Gbp,
            TrialDays = 14,
            SortOrder = 20,
            Entitlements = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["listings.concurrent"] = "25",
                ["guides.publish"] = null,
                ["messaging.basic"] = null,
                ["analytics.advanced"] = null,
                ["boosts.included.monthly"] = "1",
                ["badge.premium"] = null
            }
        };

        yield return new SubscriptionPlanSeed
        {
            Code = "professional.monthly",
            Name = "Professional",
            Description = "For members trading regularly. Unlimited listings and priority support.",
            Tier = SubscriptionTier.Professional,
            Interval = BillingInterval.Monthly,
            PriceAmount = 19.99m,
            Currency = Gbp,
            TrialDays = 14,
            SortOrder = 30,
            Entitlements = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["listings.concurrent"] = "unlimited",
                ["guides.publish"] = null,
                ["messaging.basic"] = null,
                ["analytics.advanced"] = null,
                ["boosts.included.monthly"] = "4",
                ["badge.professional"] = null,
                ["support.priority"] = null
            }
        };

        yield return new SubscriptionPlanSeed
        {
            Code = "provider.monthly",
            Name = "Provider",
            Description = "For service providers. Featured placement allowance and lead analytics.",
            Tier = SubscriptionTier.Provider,
            Interval = BillingInterval.Monthly,
            PriceAmount = 29.99m,
            Currency = Gbp,
            AudienceType = FinancialAccountType.Provider,
            SortOrder = 40,
            Entitlements = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["services.concurrent"] = "unlimited",
                ["analytics.advanced"] = null,
                ["placements.included.monthly"] = "1",
                ["badge.provider"] = null,
                ["support.priority"] = null
            }
        };

        yield return new SubscriptionPlanSeed
        {
            Code = "organization.yearly",
            Name = "Organization",
            Description = "For community organizations. Featured directory placement and member tools.",
            Tier = SubscriptionTier.Organization,
            Interval = BillingInterval.Yearly,
            PriceAmount = 149.99m,
            Currency = Gbp,
            AudienceType = FinancialAccountType.Organization,
            SortOrder = 50,
            Entitlements = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["events.concurrent"] = "unlimited",
                ["analytics.advanced"] = null,
                ["placements.included.monthly"] = "1",
                ["badge.organization"] = null,
                ["members.bulk_tools"] = null
            }
        };
    }
}
