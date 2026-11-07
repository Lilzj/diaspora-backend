using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Reports;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Sends an event or organisation report to Trust's moderation cases (7 Nov 2026).
/// <para>
/// The domain event deliberately carries no reporter. The reporter is read here, from the report row
/// that already sits behind moderator authorisation, and goes to Trust alone through the relay. The
/// organiser is never given it. <see cref="CommunityLifeReportedEventHandler"/> handles the same
/// event for the module's own published contract, which carries no reporter either.
/// </para>
/// </summary>
public sealed class CommunityLifeReportFiledEventHandler
    : DomainEventHandler<CommunityLifeReportedDomainEvent>
{
    private readonly ICommunityLifeReportRepository _reports;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly CommunityLifeContentOwners _owners;
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityLifeReportFiledEventHandler(
        ICommunityLifeReportRepository reports,
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        CommunityLifeContentOwners owners,
        CommunityLifeIntegrationEventRelay relay)
    {
        _reports = reports;
        _events = events;
        _organizations = organizations;
        _owners = owners;
        _relay = relay;
    }

    protected override async Task HandleAsync(
        CommunityLifeReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var report = await _reports.GetAsync(domainEvent.ReportId, cancellationToken);

        if (report is null)
        {
            return;
        }

        Guid owner;
        string? title;

        if (domainEvent.SubjectType == ReportSubjectType.Organization)
        {
            var organization = await _organizations.GetIncludingDeletedAsync(
                OrganizationId.From(domainEvent.SubjectId), cancellationToken);

            if (organization is null)
            {
                return;
            }

            owner = (await _owners.OfOrganizationAsync(organization, cancellationToken)).Value;
            title = organization.Name.Value;
        }
        else
        {
            var communityEvent = await _events.GetIncludingDeletedAsync(
                CommunityEventId.From(domainEvent.SubjectId), cancellationToken);

            if (communityEvent is null)
            {
                return;
            }

            owner = CommunityLifeContentOwners.OfEvent(communityEvent).Value;
            title = communityEvent.Title.Value;
        }

        await _relay.PublishToModerationCaseAsync(
            report, owner, title, domainEvent.ReportedAt, cancellationToken);
    }
}
