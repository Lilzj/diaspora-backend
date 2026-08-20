using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Eligibility;

/// <summary>
/// One condition a buyer must satisfy.
/// <para>
/// Additive: every registered rule that claims a kind is asked, and the first refusal wins. The
/// shipped rules cover the six declared kinds; a deployment adding a seventh registers a rule that
/// claims <c>Custom</c> and reads a parameter it agreed with whoever configured the product.
/// </para>
/// </summary>
public interface IProductEligibilityRule
{
    /// <summary>Which rule kind this implements.</summary>
    EligibilityRuleKind Kind { get; }

    /// <summary><paramref name="ruleValue"/> is what the operator configured on the product row.</summary>
    EligibilityDecision Evaluate(EligibilityContext context, string ruleValue);
}
