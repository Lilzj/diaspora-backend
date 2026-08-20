using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Feedback;

/// <summary>
/// What a member did with a recommendation.
/// <para>
/// <b>One command for all five interactions, and that is what keeps the quality loop coherent.</b>
/// Five commands would be five places to forget the signal, five places to look the set up, and five
/// slightly different ideas about what counts as a duplicate. The kind is a value, not a route.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.FeedbackRecord)]
[Transactional(Reason = "The feedback record, the suppression and the signal commit together.")]
[Auditable(Action = "recommendations.feedback.record")]
public sealed record RecordFeedbackCommand : ICommand<FeedbackRecordedResponse>
{
    public required string Kind { get; init; }

    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required string Surface { get; init; }

    /// <summary>The generating set. Optional; the handler recovers it when absent.</summary>
    public Guid? SetId { get; init; }

    public int? Position { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }
}
