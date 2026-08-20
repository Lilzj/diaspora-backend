using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Extensibility.Pricing;

namespace Diaspora.Modules.Financial.Infrastructure.Pricing;

/// <summary>
/// A proportion of the base amount, optionally capped and floored.
/// <para>
/// The shape every commission policy on this platform uses today: a <c>percentage</c> parameter,
/// with optional <c>minimum</c> and <c>maximum</c> in minor units. The cap is what stops a 7%
/// commission on a car sale being a number nobody intended, and the floor is what stops a 7%
/// commission on a two-pound item being zero after rounding.
/// </para>
/// </summary>
public sealed class PercentageCommissionRule : IPricingRule
{
    public const string PercentageParameter = "percentage";
    public const string MinimumParameter = "minimum_minor_units";
    public const string MaximumParameter = "maximum_minor_units";

    public int Order => 100;

    public bool CanPrice(PricingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.PolicyKind is FinancialPolicyKind.MarketplaceCommission
            or FinancialPolicyKind.HousingCommission
            or FinancialPolicyKind.ProviderCommission
            or FinancialPolicyKind.OrganizationCommission
            or FinancialPolicyKind.PlatformFee
            or FinancialPolicyKind.RevenueDistribution;
    }

    public PricingDecision? Price(
        PricingContext context, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!TryDecimal(parameters, PercentageParameter, out var percentage))
        {
            return null;
        }

        percentage = Math.Clamp(percentage, 0m, FinancialLimits.MaxCommissionPercentage);

        var amount = context.BaseAmount.MultiplyBy(percentage / 100m);

        if (TryLong(parameters, MaximumParameter, out var maximum) && amount.MinorUnits > maximum)
        {
            amount = Domain.ValueObjects.Money.FromMinorUnits(
                maximum, context.BaseAmount.Currency, context.BaseAmount.Exponent);
        }

        if (TryLong(parameters, MinimumParameter, out var minimum) && amount.MinorUnits < minimum)
        {
            amount = Domain.ValueObjects.Money.FromMinorUnits(
                Math.Min(minimum, context.BaseAmount.MinorUnits),
                context.BaseAmount.Currency,
                context.BaseAmount.Exponent);
        }

        return PricingDecision.Of(
            amount, null, $"{percentage}% of {context.BaseAmount}");
    }

    private static bool TryDecimal(
        IReadOnlyDictionary<string, string> parameters, string key, out decimal value)
    {
        value = 0m;

        return parameters.TryGetValue(key, out var raw)
            && decimal.TryParse(
                raw,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
    }

    private static bool TryLong(
        IReadOnlyDictionary<string, string> parameters, string key, out long value)
    {
        value = 0;

        return parameters.TryGetValue(key, out var raw)
            && long.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
    }
}
