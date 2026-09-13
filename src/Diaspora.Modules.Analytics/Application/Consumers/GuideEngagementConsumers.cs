using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;

namespace Diaspora.Modules.Analytics.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// GUIDE ENGAGEMENT THAT NEVER REACHES COMMUNITY
//
// Most engagement arrives from B6's four contracts (see EngagementConsumers). A guide's saves and
// comments do too: B8 delegates both to Community, which publishes them. A guide's shares and its
// helpfulness votes do not — B8 keeps those as its own counters, and until 13 Sep 2026 nothing
// crossed the boundary, so every creator's "Shares" and "Helpful votes" read zero however their
// guides were received.
//
// **Not a second copy of a Community stream.** A share made through `POST /guides/{id}/share` or a
// vote through `POST /guides/{id}/vote` never reaches Community, so consuming these alongside B6's
// contracts counts each act once. (B8's comment relay, which *does* duplicate Community's stream,
// stays unconsumed — see the architecture test.)
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Records a guide share made from the guide's own screen.</summary>
public sealed class GuideSharedAnalyticsConsumer
    : IntegrationEventHandler<GuideSharedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public GuideSharedAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        GuideSharedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentShared,
                Version: 1,
                SourceModule: "Guides",
                integrationEvent.SharedAt,
                ActorUserId: integrationEvent.SharedBy,
                ContentType: AnalyticsContentType.Guide,
                ContentId: integrationEvent.GuideId,
                OwnerUserId: integrationEvent.AuthorUserId,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records a guide helpfulness vote as a reaction.
/// <para>
/// <b>A reaction, with the vote as its key</b> — the shape B6's reactions already have, so the
/// served definitions stay true without a word changed: "Helpful votes" counts the <c>helpful</c>
/// reaction only, and "every other reaction counts towards engagement", which a "not helpful" vote
/// is. A member changing their vote records the new one; the counts are of times somebody voted,
/// as B6's reactions are of times somebody reacted.
/// </para>
/// </summary>
public sealed class GuideVoteCastAnalyticsConsumer
    : IntegrationEventHandler<GuideVoteCastIntegrationEvent>
{
    /// <summary>The reaction key the rollup counts as a helpful vote.</summary>
    private const string HelpfulReaction = "helpful";

    /// <summary>Any other vote: an engagement, and not a helpful vote.</summary>
    private const string NotHelpfulReaction = "notHelpful";

    private readonly AnalyticsIngestionService _ingestion;

    public GuideVoteCastAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        GuideVoteCastIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var reaction = string.Equals(integrationEvent.Kind, "Helpful", StringComparison.OrdinalIgnoreCase)
            ? HelpfulReaction
            : NotHelpfulReaction;

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.ContentReacted,
                Version: 1,
                SourceModule: "Guides",
                integrationEvent.CastAt,
                ActorUserId: integrationEvent.VoterUserId,
                ContentType: AnalyticsContentType.Guide,
                ContentId: integrationEvent.GuideId,
                OwnerUserId: integrationEvent.AuthorUserId,
                Qualifier: reaction,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}
