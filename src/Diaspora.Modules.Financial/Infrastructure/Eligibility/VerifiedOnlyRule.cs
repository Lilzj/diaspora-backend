using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Refuses a product to an unverified member.
/// <para>
/// The refusal names the action rather than the state, because "verify your identity" is something
/// somebody can do and "you are not verified" is not.
/// </para>
/// </summary>
public sealed class VerifiedOnlyRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.VerifiedOnly;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.IsVerified
            ? EligibilityDecision.Eligible()
            : EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "Verify your identity to buy this.");
    }
}
