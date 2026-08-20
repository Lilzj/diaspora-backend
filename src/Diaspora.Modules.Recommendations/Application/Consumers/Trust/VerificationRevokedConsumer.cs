using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Trust;

/// <summary>
/// A verification was withdrawn.
/// <para>
/// <b>Consumed because the alternative is a member keeping access to verified-only content
/// indefinitely.</b> A revocation is rare and consequential, and a personalization platform that
/// only ever heard the good news would quietly become the place where a revoked verification still
/// counted.
/// </para>
/// <para>
/// The profile is corrected on the next fold rather than immediately, which is the ordinary
/// eventual-consistency window for every consumer in the platform. The policy reads the profile, so
/// the window is bounded by the profile sweep rather than by anything this consumer controls.
/// </para>
/// </summary>
public sealed class VerificationRevokedConsumer
    : RecommendationEventConsumer<VerificationRevokedIntegrationEvent>
{
    public VerificationRevokedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Trust;

    protected override Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return RecordAsync(
            Signal(
                UserId.From(integrationEvent.UserId),
                SignalKind.Verification,
                integrationEvent.OccurredAt,
                key: "unverified"),
            cancellationToken);
    }
}
