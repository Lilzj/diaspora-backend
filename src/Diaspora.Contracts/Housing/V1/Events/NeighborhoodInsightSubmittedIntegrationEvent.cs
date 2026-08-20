using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A member said something about a neighbourhood, and it is waiting for a moderator.
/// <para>
/// <b>Submission is not publication, and this contract carries no body for that reason.</b> The
/// insight is invisible to everybody but its author and the moderation queue until an approval
/// follows; a payload containing the unreviewed text would let any bus subscriber publish a claim
/// about a real residential area that nobody has checked.
/// </para>
/// <para>
/// What it is for is queue depth and rate: a deployment where submissions climb and approvals do
/// not is one that has run out of moderator coverage.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightSubmittedIntegrationEvent : IntegrationEvent
{
    public required Guid InsightId { get; init; }

    /// <summary>The Location area the insight is about.</summary>
    public required Guid AreaId { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>Safety, Transport, CostOfLiving, Community and the rest.</summary>
    public required string Category { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }
}
