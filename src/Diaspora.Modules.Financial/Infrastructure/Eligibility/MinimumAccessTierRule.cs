using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Refuses a product to a caller below a tier.
/// <para>
/// Reads the tier from the caller's own claims rather than asking Identity, because a catalogue
/// listing that made a cross-module call would put a synchronous dependency on the busiest read path
/// in the module — and the answer is already in the token.
/// </para>
/// </summary>
public sealed class MinimumAccessTierRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.MinimumAccessTier;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Enum.TryParse<AccessTier>(ruleValue, ignoreCase: true, out var required))
        {
            // A rule nobody can parse must not silently pass. It refuses, loudly enough to be found.
            return EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "This product's eligibility is misconfigured and it cannot be offered.");
        }

        if (!Enum.TryParse<AccessTier>(context.AccessTier, ignoreCase: true, out var actual))
        {
            return EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                $"You need to reach {required} before you can buy this.");
        }

        return actual >= required
            ? EligibilityDecision.Eligible()
            : EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                $"You need to reach {required} before you can buy this.");
    }
}
