using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Messaging;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Sends every report Community files to Trust's moderation cases (7 Nov 2026).
/// <para>
/// Community takes reports on its own posts and comments, on guides (which file theirs here), and
/// on the other modules' content the report modal reaches. Each goes to Trust under the content type
/// the owning module's moderation target answers to, so a report filed here and one filed in that
/// module land on the same case. Types with no target yet (polls, members, messages) are sent too;
/// Trust logs them and the report stays in Community's own queue.
/// </para>
/// <para>
/// The owner is the author Community resolved when the report was filed. The reporter is read from
/// the report row and goes to Trust alone, which needs it to count each member once and to tell them
/// the outcome; the owner is never given it.
/// </para>
/// </summary>
public sealed class ContentReportToTrustEventHandler : DomainEventHandler<ContentReportedDomainEvent>
{
    public const string Module = "community";

    private readonly IReportRepository _reports;
    private readonly IIntegrationEventPublisher _publisher;

    public ContentReportToTrustEventHandler(IReportRepository reports, IIntegrationEventPublisher publisher)
    {
        _reports = reports;
        _publisher = publisher;
    }

    protected override async Task HandleAsync(
        ContentReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.ContentAuthorId is not { } owner || owner == Guid.Empty)
        {
            return;
        }

        var report = await _reports.GetAsync(domainEvent.ReportId, cancellationToken);

        if (report is null)
        {
            return;
        }

        await _publisher.PublishAsync(
            new ContentReportFiledIntegrationEvent
            {
                Module = Module,
                ContentType = CommunityModerationReasons.ContentTypeFor(report.Content.Type),
                ContentId = report.Content.Id,
                ContentOwnerUserId = owner,
                ReporterUserId = report.ReporterId.Value,
                ReasonCode = CommunityModerationReasons.Map(report.Reason).ToString(),
                ModuleReason = report.Reason.ToString(),
                Detail = report.Description,
                ModuleReportId = report.Id.Value,
                ContentTitle = domainEvent.ContentTitle,
                FiledAt = report.ReportedAt
            },
            cancellationToken);
    }
}
