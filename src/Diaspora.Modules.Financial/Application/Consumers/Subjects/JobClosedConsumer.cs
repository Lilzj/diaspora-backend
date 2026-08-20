using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Diaspora.Contracts.Jobs.V1.Events;

namespace Diaspora.Modules.Financial.Application.Consumers.Subjects;

/// <summary>Ends placements over a closed job posting.</summary>
public sealed class JobClosedConsumer : SubjectRetirementConsumer<JobClosedIntegrationEvent>
{
    public JobClosedConsumer(
        IPlacementRepository placements,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<JobClosedConsumer> logger)
        : base(placements, unitOfWork, clock, logger)
    {
    }

    protected override MonetizationSubjectType SubjectType => MonetizationSubjectType.Job;

    protected override string Reason => "The posting was closed.";

    protected override Guid? RetiredSubjectId(JobClosedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent.JobId;
    }
}
