namespace Diaspora.Modules.Financial.Extensibility.Eligibility;

/// <summary>
/// Runs a product's eligibility rules and returns the first refusal.
/// <para>
/// One door, so the catalogue listing and the purchase path cannot disagree about who may buy what.
/// A product shown as available and then refused at checkout is the worst version of this feature.
/// </para>
/// </summary>
public interface IProductEligibilityEvaluator
{
    EligibilityDecision Evaluate(EligibilityContext context);
}
