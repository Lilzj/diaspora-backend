using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Extensibility.Pricing;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Infrastructure.Pricing;

/// <summary>
/// Resolves the most specific active policy version and hands it to whichever rule claims it.
/// <para>
/// <b>Resolution is most-specific-first: product, then country, then module, then platform.</b> Ties
/// are broken by the higher version, which is what makes publishing version 2 take effect without
/// anything having to retire version 1 in the same transaction.
/// </para>
/// <para>
/// <b>An unresolved policy is a refusal, never a zero.</b> A commission engine that silently charges
/// nothing when its configuration is missing produces a month of sales with no revenue and nothing
/// anywhere reporting a problem — which is the class of failure the platform's handbook names as its
/// most expensive.
/// </para>
/// <para>
/// <b>The version label travels with the answer.</b> It is stamped onto the ledger entry, the
/// purchase and the placement, so a historical amount can be checked against the rule that produced
/// it rather than against whatever the catalogue says today.
/// </para>
/// </summary>
public sealed class FinancialPolicyEngine : IFinancialPolicyEngine
{
    private readonly IFinancialPolicyRepository _policies;
    private readonly IEnumerable<IPricingRule> _rules;
    private readonly IClock _clock;

    public FinancialPolicyEngine(
        IFinancialPolicyRepository policies, IEnumerable<IPricingRule> rules, IClock clock)
    {
        _policies = policies;
        _rules = rules;
        _clock = clock;
    }

    public async Task<Result<PricingDecision>> PriceAsync(
        PricingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var key = context.PolicyKey ?? DefaultKeyFor(context);

        var policy = await ResolveAsync(
            key, context.CountryCode, context.ModuleName, context.ProductCode,
            context.AsOf, cancellationToken);

        if (policy is null)
        {
            return Result.Failure<PricingDecision>(Error.Conflict(
                FinancialErrorCodes.PolicyUnresolved,
                $"No active '{key}' policy applies. A price the platform cannot resolve is a "
                + "refusal rather than a zero."));
        }

        var parameters = policy.Parameters.ToDictionary(
            parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal);

        foreach (var rule in _rules.Where(rule => rule.CanPrice(context)).OrderBy(rule => rule.Order))
        {
            if (rule.Price(context, parameters) is { } decision)
            {
                return Result.Success(decision with { PolicyVersion = policy.VersionLabel });
            }
        }

        return Result.Failure<PricingDecision>(Error.Conflict(
            FinancialErrorCodes.PolicyParameterMissing,
            $"'{policy.VersionLabel}' is active but no pricing rule could read it. Its parameters "
            + "are probably incomplete."));
    }

    public async Task<string?> ResolveVersionAsync(
        string policyKey,
        string? countryCode,
        string? moduleName,
        string? productCode,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var policy = await ResolveAsync(
            policyKey, countryCode, moduleName, productCode, asOf, cancellationToken);

        return policy?.VersionLabel;
    }

    private async Task<FinancialPolicy?> ResolveAsync(
        string policyKey,
        string? countryCode,
        string? moduleName,
        string? productCode,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return null;
        }

        var moment = asOf == default ? _clock.UtcNow : asOf;

        var candidates = await _policies.ListActiveByKeyAsync(
            policyKey.Trim().ToLowerInvariant(), cancellationToken);

        return candidates
            .Where(policy => policy.AppliesAt(moment))
            .Where(policy => policy.Matches(countryCode, moduleName, productCode))
            .OrderByDescending(policy => policy.Specificity)
            .ThenByDescending(policy => policy.Version)
            .FirstOrDefault();
    }

    /// <summary>
    /// The conventional key for a policy kind, when a caller does not name one.
    /// <para>
    /// A convenience rather than a rule: the seeded catalogue uses these names, and a deployment that
    /// wants a different one passes it explicitly.
    /// </para>
    /// </summary>
    private static string DefaultKeyFor(PricingContext context) =>
        context.PolicyKind switch
        {
            Domain.Enums.FinancialPolicyKind.MarketplaceCommission => FinancialPolicyKeys.MarketplaceCommission,
            Domain.Enums.FinancialPolicyKind.HousingCommission => FinancialPolicyKeys.HousingCommission,
            Domain.Enums.FinancialPolicyKind.ProviderCommission => FinancialPolicyKeys.ProviderCommission,
            Domain.Enums.FinancialPolicyKind.OrganizationCommission => FinancialPolicyKeys.OrganizationCommission,
            Domain.Enums.FinancialPolicyKind.PlatformFee => FinancialPolicyKeys.PlatformFee,
            Domain.Enums.FinancialPolicyKind.RefundRule => FinancialPolicyKeys.RefundWindow,
            Domain.Enums.FinancialPolicyKind.DonationRule => FinancialPolicyKeys.DonationFee,
            Domain.Enums.FinancialPolicyKind.ContributionRule => FinancialPolicyKeys.ContributionFee,
            Domain.Enums.FinancialPolicyKind.BoostPricing => FinancialPolicyKeys.BoostPricing,
            Domain.Enums.FinancialPolicyKind.PlacementPricing => FinancialPolicyKeys.PlacementPricing,
            _ => context.ProductCode is { } code ? $"product.{code}.price" : string.Empty
        };
}
