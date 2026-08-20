using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Pricing;

namespace Diaspora.Modules.Financial.Infrastructure.Pricing;

/// <summary>
/// A flat amount added regardless of the base.
/// <para>
/// Runs after <see cref="PercentageCommissionRule"/> so that a policy carrying both a percentage and
/// a flat fee is answered by the percentage; a policy carrying only a flat fee falls through to
/// here. Ordering rather than a compound rule, because the two are configured independently and a
/// rule that read both would silently do nothing when only one was set.
/// </para>
/// </summary>
public sealed class FlatFeeRule : IPricingRule
{
    public const string FeeParameter = "flat_fee_minor_units";

    public int Order => 120;

    public bool CanPrice(PricingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.PolicyKind is FinancialPolicyKind.PlatformFee
            or FinancialPolicyKind.DonationRule
            or FinancialPolicyKind.ContributionRule;
    }

    public PricingDecision? Price(
        PricingContext context, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!parameters.TryGetValue(FeeParameter, out var raw)
            || !long.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var minorUnits))
        {
            return null;
        }

        // A fee larger than what is being charged would leave the payer owing money on a donation.
        var capped = Math.Min(minorUnits, context.BaseAmount.MinorUnits);

        return PricingDecision.Of(
            Money.FromMinorUnits(capped, context.BaseAmount.Currency, context.BaseAmount.Exponent),
            null,
            "A flat fee from policy.");
    }
}
