using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Platform.Messaging;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Sends a job report to Trust's moderation cases (7 Nov 2026).
/// <para>
/// The domain event deliberately carries no reporter. The reporter is read here, from the report row
/// that already sits behind moderator authorisation, and goes to Trust alone, which needs it to count
/// each member once and to tell them the outcome. The employer is never given it.
/// </para>
/// </summary>
public sealed class JobPostingReportedEventHandler : DomainEventHandler<JobPostingReportedDomainEvent>
{
    private readonly IJobReportRepository _reports;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IIntegrationEventPublisher _publisher;

    public JobPostingReportedEventHandler(
        IJobReportRepository reports,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IIntegrationEventPublisher publisher)
    {
        _reports = reports;
        _postings = postings;
        _employers = employers;
        _publisher = publisher;
    }

    protected override async Task HandleAsync(
        JobPostingReportedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var report = await _reports.GetAsync(domainEvent.ReportId, cancellationToken);

        if (report is null)
        {
            return;
        }

        var posting = await _postings.GetForReadAsync(domainEvent.PostingId, cancellationToken);
        var employer = await _employers.GetForReadAsync(domainEvent.EmployerId, cancellationToken);

        var owner = employer?.OwnerUserId.Value ?? posting?.CreatedByUserId.Value;

        if (owner is null)
        {
            return;
        }

        await _publisher.PublishAsync(
            new ContentReportFiledIntegrationEvent
            {
                Module = "jobs",
                ContentType = ModerationContentTypes.Job,
                ContentId = domainEvent.PostingId.Value,
                ContentOwnerUserId = owner.Value,
                ReporterUserId = report.ReportedBy.Value,
                ReasonCode = JobModerationReasons.Map(report.Reason).ToString(),
                ModuleReason = report.Reason.ToString(),
                Detail = report.Detail,
                ModuleReportId = report.Id.Value,
                ContentTitle = posting?.Title.Value,
                FiledAt = domainEvent.ReportedAt
            },
            cancellationToken);
    }
}
