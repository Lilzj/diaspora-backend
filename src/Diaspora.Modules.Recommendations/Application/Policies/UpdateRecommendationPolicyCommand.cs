using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Policies;

/// <summary>
/// Changes one recommendation policy.
/// <para>
/// <b>Audited, because a policy change is invisible and enormous.</b> Disabling the
/// country-visibility policy does not fail, does not log an error and does not change any response
/// shape — it silently starts showing every member content from every country. The audit entry and
/// the published event are the only two records that it happened.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.PoliciesManage)]
[Transactional(Reason = "A policy change is one decision across its state, scope and parameters.")]
[Auditable(Action = "recommendations.policy.update")]
public sealed record UpdateRecommendationPolicyCommand : ICommand<RecommendationPolicyResponse>
{
    public required Guid PolicyId { get; init; }

    public bool? IsEnabled { get; init; }

    public int? Order { get; init; }

    public decimal? DemotionFactor { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string>? Surfaces { get; init; }

    public IReadOnlyList<string>? TargetTypes { get; init; }

    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
