using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Policies;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Modules.Recommendations.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Runs every enabled policy over a pool, candidate-major, once.
/// <para>
/// <b>Candidate-major with an early exit, which is the whole performance argument.</b> Nine rules
/// over four hundred candidates is thirty-six hundred evaluations rule-major and considerably fewer
/// candidate-major, because most excluded candidates are excluded by the first cheap rule that looks
/// at them and the remaining eight never run.
/// </para>
/// <para>
/// <b>A misconfigured rule is skipped, never fatal.</b> A catalogue row naming a key nothing
/// implements is logged once per generation and passed over: a member's feed should not go blank
/// because an operator mistyped a policy key, and the startup validator has already made that loud
/// in the place where it can be fixed.
/// </para>
/// <para>
/// <b>The per-member sets are assembled here, once.</b> Suppressed targets and owned targets are two
/// reads per generation rather than two per candidate — see <c>PolicyContext</c> for why the rules
/// cannot fetch their own.
/// </para>
/// </summary>
public sealed class RecommendationPolicyEvaluator : IRecommendationPolicyEvaluator
{
    private const string PolicyCacheKey = "recommendations:policies:enabled";

    private readonly IRecommendationPolicyRepository _policies;
    private readonly RecommendationPolicyRuleRegistry _rules;
    private readonly IRecommendationFeedbackRepository _feedback;
    private readonly IRecommendationCandidateRepository _candidates;
    private readonly IRecommendationCache _cache;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly ILogger<RecommendationPolicyEvaluator> _logger;

    public RecommendationPolicyEvaluator(
        IRecommendationPolicyRepository policies,
        RecommendationPolicyRuleRegistry rules,
        IRecommendationFeedbackRepository feedback,
        IRecommendationCandidateRepository candidates,
        IRecommendationCache cache,
        IOptions<RecommendationOptions> options,
        ILogger<RecommendationPolicyEvaluator> logger)
    {
        _policies = policies;
        _rules = rules;
        _feedback = feedback;
        _candidates = candidates;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<PolicyEvaluationResult> EvaluateAsync(
        IReadOnlyList<RecommendationCandidate> candidates,
        PersonalizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        if (candidates.Count == 0)
        {
            return PolicyEvaluationResult.Unfiltered(candidates);
        }

        var configured = await LoadPoliciesAsync(cancellationToken);

        var applicable = configured
            .Where(policy => policy.AppliesTo(context.Surface))
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.Key, StringComparer.Ordinal)
            .ToList();

        if (applicable.Count == 0)
        {
            return PolicyEvaluationResult.Unfiltered(candidates);
        }

        var suppressed = await LoadSuppressedAsync(context, cancellationToken);
        var owned = await LoadOwnedAsync(candidates, context, cancellationToken);

        var allowed = new List<RecommendationCandidate>(candidates.Count);
        var demotions = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var exclusions = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var factor = 1m;
            var excludedBy = (string?)null;

            foreach (var policy in applicable)
            {
                if (!policy.AppliesTo(candidate.Target.TargetType))
                {
                    continue;
                }

                var rule = _rules.Find(policy.Key);

                if (rule is null)
                {
                    // Logged once per generation rather than per candidate: four hundred identical
                    // lines would bury whatever else went wrong in the same request.
                    continue;
                }

                var policyContext = new PolicyContext
                {
                    Personalization = context,
                    Configuration = policy,
                    SuppressedTargetKeys = suppressed,
                    OwnedTargetKeys = owned
                };

                var decision = rule.Evaluate(candidate, policyContext);

                if (decision.IsExcluded)
                {
                    excludedBy = policy.Key;
                    break;
                }

                if (decision.Factor < 1m)
                {
                    factor *= decision.Factor;
                }
            }

            if (excludedBy is not null)
            {
                exclusions[excludedBy] = exclusions.GetValueOrDefault(excludedBy) + 1;
                RecommendationTelemetry.PolicyExclusions.Add(
                    1, new KeyValuePair<string, object?>("policy", excludedBy));

                continue;
            }

            allowed.Add(candidate);

            if (factor < 1m)
            {
                demotions[candidate.Target.Key] = decimal.Round(factor, 6);
            }
        }

        WarnAboutUnimplemented(applicable);

        return new PolicyEvaluationResult
        {
            Allowed = allowed,
            DemotionFactors = demotions,
            ExclusionsByPolicy = exclusions
        };
    }

    /// <summary>
    /// The enabled policy rows, cached.
    /// <para>
    /// Read on every generation and written twice a year — the textbook case for a cache, and the
    /// catalogue write path invalidates by prefix so a retune takes effect immediately rather than
    /// whenever the entry happens to expire.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<RecommendationPolicy>> LoadPoliciesAsync(
        CancellationToken cancellationToken) =>
        await _cache.GetOrCreateAsync(
            PolicyCacheKey,
            _options.Value.CacheLifetime,
            async token => (await _policies.ListEnabledAsync(token)).ToList(),
            cancellationToken);

    private async Task<IReadOnlySet<string>> LoadSuppressedAsync(
        PersonalizationContext context, CancellationToken cancellationToken)
    {
        var suppressed = await _feedback.ListAllSuppressedAsync(
            context.UserId, context.AsOf, cancellationToken);

        return suppressed
            .Select(entry => $"{entry.TargetType}:{entry.TargetId:N}")
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Targets the member owns, for the types actually in the pool.
    /// <para>
    /// Scoped to the pool's types rather than every type, because a home-feed pool holds four types
    /// and querying the other eight would be eight reads for nothing.
    /// </para>
    /// </summary>
    private async Task<IReadOnlySet<string>> LoadOwnedAsync(
        IReadOnlyList<RecommendationCandidate> candidates,
        PersonalizationContext context,
        CancellationToken cancellationToken)
    {
        var types = candidates
            .Select(candidate => candidate.Target.TargetType)
            .Distinct()
            .ToList();

        var owned = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            var ids = await _candidates.ListOwnedTargetIdsAsync(context.UserId, type, cancellationToken);

            foreach (var id in ids)
            {
                owned.Add($"{type}:{id:N}");
            }
        }

        return owned;
    }

    private void WarnAboutUnimplemented(IReadOnlyList<RecommendationPolicy> applicable)
    {
        var unimplemented = applicable
            .Where(policy => !_rules.IsImplemented(policy.Key))
            .Select(policy => policy.Key)
            .ToList();

        if (unimplemented.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "{Count} enabled recommendation policies have no registered implementation and were "
            + "skipped: {Keys}. Content these rules were meant to withhold is being recommended.",
            unimplemented.Count,
            string.Join(", ", unimplemented));
    }
}
