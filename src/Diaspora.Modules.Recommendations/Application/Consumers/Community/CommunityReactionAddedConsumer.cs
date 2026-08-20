using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Community;

/// <summary>
/// A member reacted to something.
/// <para>
/// <b>The sentiment is read, and a negative reaction is not recorded as engagement.</b> B6's reaction
/// catalogue carries a sentiment precisely so consumers do not have to guess what a reaction key
/// means, and treating a negative one as interest would teach the platform to recommend more of what
/// a member disliked — the exact failure the sentiment field exists to prevent.
/// </para>
/// <para>
/// A negative reaction is also not recorded as a dismissal. Disagreeing with something is not the
/// same as not wanting to see it, and conflating the two would make disagreement a way of
/// accidentally hiding a topic.
/// </para>
/// </summary>
public sealed class CommunityReactionAddedConsumer
    : RecommendationEventConsumer<CommunityReactionAddedIntegrationEvent>
{
    /// <summary>B6's sentiment for a reaction that means the opposite of interest.</summary>
    private const string NegativeSentiment = "negative";

    public CommunityReactionAddedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Community;

    protected override Task HandleAsync(
        CommunityReactionAddedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Not engagement, and not a dismissal either. See the class summary.
        if (string.Equals(
                integrationEvent.Sentiment, NegativeSentiment, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var targetType = ContentTypeMap.Translate(integrationEvent.ContentType);

        if (targetType is null)
        {
            return Task.CompletedTask;
        }

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.UserId),
                SignalKind.Engaged,
                integrationEvent.OccurredAt,
                targetType,
                integrationEvent.ContentId),
            cancellationToken);
    }
}
