using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Withholds a product in named countries.
/// <para>
/// The counterpart to the allow list, and both exist because they fail differently: an allow list
/// left empty offers nothing, a deny list left empty offers everything. An operator restricting one
/// market wants the second; an operator launching in one market wants the first.
/// </para>
/// </summary>
public sealed class CountryDenyListRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.CountryDenyList;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        var denied = (ruleValue ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return context.CountryCode is { } country
            && denied.Contains(country.Trim().ToUpperInvariant())
            ? EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "This is not available in your country.")
            : EligibilityDecision.Eligible();
    }
}
