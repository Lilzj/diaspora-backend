using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Events;

namespace Diaspora.Modules.Jobs.Application.EventHandlers;

/// <summary>
/// Tells the platform a report was upheld, so Trust pays the reporter (REP-13). The report names
/// the employer's profile; its owner is read here so Trust can refuse a self-report.
/// </summary>
public sealed class JobReportUpheldEventHandler : DomainEventHandler<JobReportUpheldDomainEvent>
{
    private readonly JobIntegrationEventRelay _relay;
    private readonly IEmployerProfileRepository _employers;

    public JobReportUpheldEventHandler(JobIntegrationEventRelay relay, IEmployerProfileRepository employers)
    {
        _relay = relay;
        _employers = employers;
    }

    protected override async Task HandleAsync(
        JobReportUpheldDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var employer = await _employers.GetForReadAsync(domainEvent.EmployerId, cancellationToken);

        await _relay.PublishAsync(domainEvent, employer?.OwnerUserId.Value, cancellationToken);
    }
}
