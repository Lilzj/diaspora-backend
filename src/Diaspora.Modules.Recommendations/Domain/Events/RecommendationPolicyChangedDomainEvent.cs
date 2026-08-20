using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// A recommendation policy was created, changed, enabled or disabled.
/// <para>
/// <b>Published because a policy change is invisible from the outside and enormous in effect.</b>
/// Disabling the country-visibility policy does not fail, does not log an error and does not change
/// any response shape — it silently starts showing every member content from every country. The
/// event is what puts that on an audit trail somebody reads.
/// </para>
/// </summary>
public sealed record RecommendationPolicyChangedDomainEvent : DomainEvent
{
    public required RecommendationPolicyId PolicyId { get; init; }

    public required string PolicyKey { get; init; }

    public required RecommendationPolicyKind Kind { get; init; }

    public required PolicyEffect Effect { get; init; }

    public required bool IsEnabled { get; init; }

    /// <summary><c>created</c>, <c>updated</c>, <c>enabled</c> or <c>disabled</c>.</summary>
    public required string ChangeType { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
