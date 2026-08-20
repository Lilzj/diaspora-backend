using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>Offers a product only in the countries the operator listed.</summary>
public sealed class CountryAllowListRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.CountryAllowList;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        var allowed = Split(ruleValue);

        if (allowed.Count == 0)
        {
            return EligibilityDecision.Eligible();
        }

        return context.CountryCode is { } country
            && allowed.Contains(country.Trim().ToUpperInvariant())
            ? EligibilityDecision.Eligible()
            : EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "This is not available in your country yet.");
    }

    private static HashSet<string> Split(string? value) =>
        new(
            (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => code.ToUpperInvariant()),
            StringComparer.Ordinal);
}
