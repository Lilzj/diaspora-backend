using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Jobs;

/// <summary>
/// A vacancy was filed away.
/// <para>
/// Distinct from closing — an employer can archive a posting that never closed — and both must
/// withdraw. Subscribing to only one of them is the sort of gap that leaves a handful of
/// unrecommendable jobs in every pool for months.
/// </para>
/// </summary>
public sealed class JobArchivedConsumer : RecommendationEventConsumer<JobArchivedIntegrationEvent>
{
    public JobArchivedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Jobs;

    protected override Task HandleAsync(
        JobArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.Job, integrationEvent.JobId, cancellationToken);
    }
}
