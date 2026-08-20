using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Observability;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes an approved insight, and nothing else.
/// <para>
/// <b>The asymmetry is the moderation model expressed on the bus.</b> An approval carries the text,
/// because a moderator has read it. A rejection is not published at all: a refused insight's
/// existence is between its author and the moderator, and broadcasting it would tell every
/// subscriber what a member tried to say about somewhere they live.
/// </para>
/// <para>
/// The body is read back from the aggregate rather than carried on the domain event, because the
/// domain event is deliberately thin — it reports a decision, and the decision is about an insight
/// that is still there to be read.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightDecidedEventHandler
    : DomainEventHandler<NeighborhoodInsightDecidedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;
    private readonly INeighborhoodInsightRepository _insights;

    public NeighborhoodInsightDecidedEventHandler(
        HousingIntegrationEventRelay relay, INeighborhoodInsightRepository insights)
    {
        _relay = relay;
        _insights = insights;
    }

    protected override async Task HandleAsync(
        NeighborhoodInsightDecidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.CurrentStatus != NeighborhoodInsightStatus.Approved)
        {
            return;
        }

        var insight = await _insights.GetForReadAsync(
            domainEvent.InsightId, cancellationToken);

        if (insight is null)
        {
            return;
        }

        await _relay.PublishAsync(
            domainEvent, insight.Body, insight.Label, cancellationToken);

        HousingTelemetry.InsightsApproved.Add(
            1,
            new KeyValuePair<string, object?>("category", domainEvent.Category.ToString()));
    }
}
