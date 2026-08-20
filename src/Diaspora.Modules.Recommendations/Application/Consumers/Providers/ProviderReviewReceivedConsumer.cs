using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Providers;

/// <summary>
/// Somebody reviewed a provider.
/// <para>
/// <b>Two facts in one event, and they are about different people.</b> The reviewer used the
/// provider, which is the strongest engagement signal this vertical produces — you do not review a
/// plumber you did not hire. And the rating is a quality measurement about the provider, which is
/// the only quality input the providers surface has.
/// </para>
/// <para>
/// <b>The quality score is folded, not replaced, and the choice matters.</b> This event carries one
/// review's rating rather than a running average, so setting the score outright would make a
/// provider's quality equal to whatever the most recent reviewer thought — one bad Tuesday would
/// undo a hundred good reviews. The exponential fold weights history without needing this module to
/// keep a count.
/// </para>
/// </summary>
public sealed class ProviderReviewReceivedConsumer
    : RecommendationEventConsumer<ProviderReviewReceivedIntegrationEvent>
{
    /// <summary>How much one review moves a provider's quality. Deliberately small.</summary>
    private const decimal FoldRate = 0.2m;

    /// <summary>The scale ratings arrive on.</summary>
    private const decimal MaximumRating = 5m;

    private readonly Domain.Candidates.IRecommendationCandidateRepository _candidates;

    public ProviderReviewReceivedConsumer(
        CandidateProjector projector,
        ISignalStore signals,
        Domain.Candidates.IRecommendationCandidateRepository candidates)
        : base(projector, signals) =>
        _candidates = candidates;

    protected override SignalSource Source => SignalSource.Providers;

    protected override async Task HandleAsync(
        ProviderReviewReceivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // You do not review a plumber you did not hire. See the class summary.
        await RecordAsync(
            Signal(
                UserId.From(integrationEvent.ReviewerId),
                SignalKind.Transacted,
                integrationEvent.ReceivedAt,
                RecommendationTargetType.Provider,
                integrationEvent.ProviderId),
            cancellationToken);

        var existing = await _candidates.FindAsync(
            RecommendationTargetType.Provider, integrationEvent.ProviderId, cancellationToken);

        if (existing is null)
        {
            // A review for a provider this module has never seen published. Nothing to fold into,
            // and inventing a projection from a review would create a candidate with no title.
            return;
        }

        var rating = Math.Clamp(integrationEvent.Rating / MaximumRating, 0m, 1m);
        var folded = existing.QualityScore + ((rating - existing.QualityScore) * FoldRate);

        await Projector.ProjectAsync(
            new CandidateProjectionRequest
            {
                TargetType = RecommendationTargetType.Provider,
                TargetId = integrationEvent.ProviderId,
                SourceModule = SourceModule,
                QualityScore = decimal.Round(folded, 6),
                EngagementCount = existing.EngagementCount + 1,
                SourceUpdatedAt = integrationEvent.ReceivedAt
            },
            cancellationToken);
    }
}
