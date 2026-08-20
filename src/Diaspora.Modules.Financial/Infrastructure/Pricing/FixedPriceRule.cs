using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Pricing;

namespace Diaspora.Modules.Financial.Infrastructure.Pricing;

/// <summary>
/// A flat figure the policy names outright.
/// <para>
/// What a country override looks like: the same product, a different number in Lagos and in London,
/// each a policy version with its own effective date. The parameter is minor units rather than a
/// decimal so that an operator typing a price cannot introduce a rounding question.
/// </para>
/// </summary>
public sealed class FixedPriceRule : IPricingRule
{
    public const string AmountParameter = "amount_minor_units";
    public const string CurrencyParameter = "currency";

    public int Order => 110;

    public bool CanPrice(PricingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.PolicyKind is FinancialPolicyKind.ProductPricing
            or FinancialPolicyKind.SubscriptionPricing
            or FinancialPolicyKind.PlacementPricing
            or FinancialPolicyKind.BoostPricing
            or FinancialPolicyKind.CampaignPricing;
    }

    public PricingDecision? Price(
        PricingContext context, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!parameters.TryGetValue(AmountParameter, out var raw)
            || !long.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var minorUnits))
        {
            return null;
        }

        var currency = parameters.TryGetValue(CurrencyParameter, out var configured)
            ? Money.NormalizeCurrency(configured) ?? context.BaseAmount.Currency
            : context.BaseAmount.Currency;

        // A price configured in another currency is not converted — it simply does not apply, and
        // the engine falls through to whatever else can answer. Converting here would be the one
        // place the platform's no-conversion rule quietly broke.
        if (!string.Equals(currency, context.BaseAmount.Currency, StringComparison.Ordinal))
        {
            return null;
        }

        return PricingDecision.Of(
            Money.FromMinorUnits(minorUnits, currency, context.BaseAmount.Exponent),
            null,
            "A fixed price from policy.");
    }
}
