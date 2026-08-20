using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Jobs;

/// <summary>
/// A candidate applied for a job.
/// <para>
/// <b>The strongest behavioural signal the Jobs module produces, and one of the strongest on the
/// platform.</b> An application is an evening's work: a CV tailored, a covering letter written, a
/// decision made. Weighted far above a view for exactly that reason — a member who applied to one
/// job and viewed thirty should be recommended things like the one.
/// </para>
/// <para>
/// <b>The category is recorded, and PSR-1 is why.</b> This contract carried identifiers and no job
/// attributes until then, so the affinity an application produced was against the one posting
/// rather than against what the job was about — a member who applied to three plumbing jobs was
/// never observed to be interested in plumbing. DEBT-006 enriched the contract at the source,
/// which was the correct fix rather than a call back into Jobs.
/// </para>
/// <para>
/// <b>Null is normal and is not a failure.</b> An application submitted before the enrichment
/// carries no key, and the signal is still recorded — an affinity for the posting, which is what
/// this consumer produced for its whole life until now. The shape is Marketplace's: a key, an
/// interest kind, and the country as context.
/// </para>
/// </summary>
public sealed class ApplicationSubmittedConsumer
    : RecommendationEventConsumer<ApplicationSubmittedIntegrationEvent>
{
    public ApplicationSubmittedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Jobs;

    protected override Task HandleAsync(
        ApplicationSubmittedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.ApplicantUserId),
                SignalKind.Applied,
                integrationEvent.SubmittedAt,
                RecommendationTargetType.Job,
                integrationEvent.JobId,
                integrationEvent.CategoryKey,
                integrationEvent.CategoryKey is null ? null : InterestKind.Category,
                SignalContext.Create(integrationEvent.CountryCode)),
            cancellationToken);
    }
}
