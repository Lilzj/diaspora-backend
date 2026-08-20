using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Jobs;

/// <summary>
/// A vacancy stopped accepting applications.
/// <para>
/// <b>The withdrawal half of the projection, and it matters more than the publication half.</b> A
/// job that was never projected is invisible; a job that stays projected after it closes is actively
/// harmful — it costs a candidate an evening and teaches them the feed is unreliable.
/// </para>
/// </summary>
public sealed class JobClosedConsumer : RecommendationEventConsumer<JobClosedIntegrationEvent>
{
    public JobClosedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Jobs;

    protected override Task HandleAsync(
        JobClosedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Projector.WithdrawAsync(
            RecommendationTargetType.Job, integrationEvent.JobId, cancellationToken);
    }
}
