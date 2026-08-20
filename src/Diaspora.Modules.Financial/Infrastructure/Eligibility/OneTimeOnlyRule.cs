using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Refuses a second purchase of a product for the same subject.
/// <para>
/// The introductory-offer rule. Reads a fact the caller was given rather than counting rows itself,
/// so the same decision is reached by the catalogue listing and the purchase path from one query.
/// </para>
/// </summary>
public sealed class OneTimeOnlyRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.OneTimeOnly;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.HasPurchasedBefore
            ? EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "You have already bought this.")
            : EligibilityDecision.Eligible();
    }
}
