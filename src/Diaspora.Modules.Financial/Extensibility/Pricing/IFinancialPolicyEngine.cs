using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Extensibility.Pricing;

/// <summary>
/// Resolves a versioned policy and turns it into a number.
/// <para>
/// <b>Every price and every rate on this platform comes through here.</b> Commission, platform fees,
/// placement prices, boost prices, subscription prices, campaign pricing and country overrides are
/// all the same operation: find the most specific active version for this key and scope, hand its
/// parameters to whichever rule claims it, and return the amount with the version that produced it.
/// </para>
/// <para>
/// <b>An unresolved policy is a refusal, never a zero.</b> A commission engine that silently charges
/// nothing when its configuration is missing is worse than one that stops — it produces a month of
/// sales with no revenue and nothing anywhere reporting a problem.
/// </para>
/// </summary>
public interface IFinancialPolicyEngine
{
    /// <summary>The amount this policy decides for this context.</summary>
    Task<Result<PricingDecision>> PriceAsync(
        PricingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most specific active version of a key for a scope, as its label — for callers that need
    /// to stamp a version without computing an amount.
    /// </summary>
    Task<string?> ResolveVersionAsync(
        string policyKey,
        string? countryCode,
        string? moduleName,
        string? productCode,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}
