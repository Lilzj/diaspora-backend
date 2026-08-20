using Diaspora.Contracts.Knowledge.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Knowledge;

/// <summary>
/// A learning path changed.
/// <para>
/// <b>One event carries creation, update, publication and retirement, so this consumer has to read
/// the status rather than the verb.</b> That is unusual for this platform — most verticals publish a
/// separate contract per lifecycle step — and it is why the status check is explicit here rather
/// than expressed as two consumers.
/// </para>
/// <para>
/// <b>The path's key becomes a tag, which is how "continue learning" works at all.</b> A member's
/// progress signal carries the same key, so the interest strategy matches the path they are partway
/// through against the profile affinity their progress created — no join, no per-vertical special
/// case.
/// </para>
/// </summary>
public sealed class LearningPathUpdatedConsumer
    : RecommendationEventConsumer<LearningPathUpdatedIntegrationEvent>
{
    /// <summary>The status at which a path may be recommended.</summary>
    private const string PublishedStatus = "Published";

    public LearningPathUpdatedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Knowledge;

    protected override async Task HandleAsync(
        LearningPathUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // One contract, four lifecycle steps. See the class summary.
        if (!string.Equals(integrationEvent.Status, PublishedStatus, StringComparison.OrdinalIgnoreCase))
        {
            await Projector.WithdrawAsync(
                RecommendationTargetType.LearningPath,
                integrationEvent.LearningPathId,
                cancellationToken);

            return;
        }

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.LearningPath,
                TargetId = integrationEvent.LearningPathId,
                SourceModule = SourceModule,
                Title = integrationEvent.Title,
                Slug = integrationEvent.Key,
                CategoryKey = integrationEvent.Key,
                CountryCode = integrationEvent.CountryCode,
                PopularityScore = 0.4m,
                QualityScore = 0.6m,
                Visibility = CandidateVisibility.Public,
                PublishedAt = integrationEvent.ChangedAt,
                SourceUpdatedAt = integrationEvent.ChangedAt,

                // The key as a tag, so progress signals meet the path in one vocabulary. See the
                // class summary.
                Attributes =
                [
                    (InterestKind.Tag, integrationEvent.Key),
                    (InterestKind.Tag, $"steps-{integrationEvent.StepCount}")
                ],
                ReplaceAttributes = true
            },
            cancellationToken);
    }
}
