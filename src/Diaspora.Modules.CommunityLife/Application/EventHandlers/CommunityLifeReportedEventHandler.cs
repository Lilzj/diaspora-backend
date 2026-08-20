using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Somebody reported an event or a body.
/// <para>
/// <b>Whether it was flagged is read off the subject rather than carried on the event.</b> The
/// threshold is configuration and the domain refuses to know about configuration, so the aggregate
/// applies it and this reads the outcome — which also means a subject flagged by a moderator by hand
/// reports as flagged, not as one report short of it.
/// </para>
/// </summary>
public sealed class CommunityLifeReportedEventHandler
    : DomainEventHandler<CommunityLifeReportedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;

    public CommunityLifeReportedEventHandler(
        CommunityLifeIntegrationEventRelay relay,
        ICommunityEventRepository events,
        IOrganizationRepository organizations)
    {
        _relay = relay;
        _events = events;
        _organizations = organizations;
    }

    protected override async Task HandleAsync(
        CommunityLifeReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var state = domainEvent.SubjectType == ReportSubjectType.Organization
            ? (await _organizations.GetAsync(
                OrganizationId.From(domainEvent.SubjectId), cancellationToken))?.ModerationState
            : (await _events.GetAsync(
                CommunityEventId.From(domainEvent.SubjectId), cancellationToken))?.ModerationState;

        var wasFlagged = state is CommunityLifeModerationState.Flagged
            or CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended;

        await _relay.PublishAsync(domainEvent, wasFlagged, cancellationToken);
    }
}
