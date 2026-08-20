using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Eligibility;

namespace Diaspora.Modules.Financial.Infrastructure.Eligibility;

/// <summary>
/// Offers a product only to holders of a plan.
/// <para>
/// An empty rule value means "any subscription at all", which is the common case and saves an
/// operator listing every plan code whenever a plan is added.
/// </para>
/// </summary>
public sealed class SubscriptionRequiredRule : IProductEligibilityRule
{
    public EligibilityRuleKind Kind => EligibilityRuleKind.SubscriptionRequired;

    public EligibilityDecision Evaluate(EligibilityContext context, string ruleValue)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SubscriptionPlanCode is not { } held)
        {
            return EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "This is part of a subscription plan.");
        }

        var required = (ruleValue ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        return required.Count == 0 || required.Contains(held.ToLowerInvariant())
            ? EligibilityDecision.Eligible()
            : EligibilityDecision.Refused(
                FinancialErrorCodes.ProductNotEligible,
                "Your plan does not include this.");
    }
}
