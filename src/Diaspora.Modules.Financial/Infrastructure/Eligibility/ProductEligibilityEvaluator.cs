using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Runs a product's rules and returns the first refusal.
/// <para>
/// <b>One door, so the catalogue listing and the purchase path cannot disagree.</b> A product shown
/// as available and then refused at checkout is the worst version of this feature, and it is what
/// happens the moment two code paths each decide eligibility for themselves.
/// </para>
/// <para>
/// <b>A configured rule with no implementation refuses.</b> Passing would mean a product restricted
/// to two countries quietly becoming available everywhere the day somebody removed the rule class —
/// a failure with no error anywhere, which is the platform's most expensive recurring shape.
/// </para>
/// </summary>
public sealed class ProductEligibilityEvaluator : IProductEligibilityEvaluator
{
    private readonly IReadOnlyList<IProductEligibilityRule> _rules;

    public ProductEligibilityEvaluator(IEnumerable<IProductEligibilityRule> rules) =>
        _rules = [.. rules];

    public EligibilityDecision Evaluate(EligibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var configured in context.Product.Eligibility)
        {
            var rule = _rules.FirstOrDefault(candidate => candidate.Kind == configured.Kind);

            if (rule is null)
            {
                return EligibilityDecision.Refused(
                    FinancialErrorCodes.ProductNotEligible,
                    "This product's eligibility is misconfigured and it cannot be offered.");
            }

            var decision = rule.Evaluate(context, configured.Value);

            if (!decision.IsEligible)
            {
                return decision;
            }
        }

        return EligibilityDecision.Eligible();
    }
}
