using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility.Handlers;

/// <summary>
/// A suspended account does nothing.
/// <para>
/// Runs first (<see cref="Order"/> −100) so that a suspended member is told they are
/// suspended rather than told they need 150 more reputation points — which would be true,
/// irrelevant, and actively misleading about what they need to do next.
/// </para>
/// </summary>
public sealed class SuspendedAccountRule : ITrustRule
{
    public string Module => TrustModuleMetadata.Name;

    /// <summary>Empty: applies to every capability.</summary>
    public IReadOnlyCollection<string> AppliesTo => [];

    public int Order => -100;

    public TrustRuleVerdict Evaluate(TrustRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Health == AccountHealth.Suspended
            ? TrustRuleVerdict.Deny(
                "Your account has been suspended for violating our Community Guidelines.",
                EligibilityCode.HEALTH_RESTRICTED)
            : TrustRuleVerdict.Allow;
    }
}

/// <summary>
/// A restricted account may read and react, but not publish.
/// <para>
/// The capability list mirrors the frontend's restricted-health branch in
/// <c>TrustEngine.evaluateAction</c> exactly, so moving the check server-side does not
/// silently widen or narrow what a restricted member can do.
/// </para>
/// </summary>
public sealed class RestrictedAccountRule : ITrustRule
{
    public string Module => TrustModuleMetadata.Name;

    public IReadOnlyCollection<string> AppliesTo =>
    [
        TrustCapabilities.PublishGuide,
        TrustCapabilities.PostJob,
        TrustCapabilities.ListHousing,
        TrustCapabilities.SellItem,
        TrustCapabilities.PublishUpdate,
        TrustCapabilities.CreateEvent,
        TrustCapabilities.CreateOrganization,
        TrustCapabilities.Comment
    ];

    public int Order => -90;

    public TrustRuleVerdict Evaluate(TrustRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Health == AccountHealth.Restricted
            ? TrustRuleVerdict.Deny(
                "Your account is under posting restrictions. Please contact support or "
                + "resolve your active appeals.",
                EligibilityCode.HEALTH_RESTRICTED)
            : TrustRuleVerdict.Allow;
    }
}

/// <summary>
/// High-risk members do not publish until a human has looked.
/// <para>
/// The one rule with no counterpart on the client, and deliberately so: the frontend computes
/// a risk score but never gates on it, because a score computed on the device is a score the
/// device can set to zero. Now that risk is computed server-side it can actually hold
/// something back.
/// </para>
/// </summary>
public sealed class HighRiskPublishingRule : ITrustRule
{
    public string Module => TrustModuleMetadata.Name;

    public IReadOnlyCollection<string> AppliesTo =>
    [
        TrustCapabilities.PublishGuide,
        TrustCapabilities.PostJob,
        TrustCapabilities.ListHousing,
        TrustCapabilities.SellItem
    ];

    public int Order => -50;

    public TrustRuleVerdict Evaluate(TrustRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RiskLevel == RiskLevel.High
            ? TrustRuleVerdict.Deny(
                "This action is temporarily unavailable while we review recent activity on "
                + "your account.",
                EligibilityCode.HEALTH_RESTRICTED)
            : TrustRuleVerdict.Allow;
    }
}
