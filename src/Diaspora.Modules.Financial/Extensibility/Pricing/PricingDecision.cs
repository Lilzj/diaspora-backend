using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Extensibility.Pricing;

/// <summary>
/// What a policy decided, and which version decided it.
/// <para>
/// <b><see cref="PolicyVersion"/> is not optional in practice.</b> It is stamped onto the ledger
/// entry, the purchase and the placement; without it a historical amount can only be checked by
/// recomputing it from today's configuration, which produces a plausible answer and a wrong one.
/// </para>
/// </summary>
public sealed record PricingDecision
{
    public required Money Amount { get; init; }

    /// <summary><c>marketplace.commission@2</c>. Null only when no policy was consulted.</summary>
    public string? PolicyVersion { get; init; }

    /// <summary>How the figure was arrived at, in words. Shown on an operator surface.</summary>
    public string? Explanation { get; init; }

    public static PricingDecision Of(Money amount, string? policyVersion, string? explanation = null) =>
        new() { Amount = amount, PolicyVersion = policyVersion, Explanation = explanation };
}
