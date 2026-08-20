using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Trust;

/// <summary>
/// The Trust Engine verified somebody.
/// <para>
/// <b>Changes what a member may be recommended, not just how they are ranked.</b> The
/// trust-restriction policy excludes verified-only content for an unverified member, so this event
/// is what opens a part of the platform rather than merely improving its ordering.
/// </para>
/// <para>
/// <b>A partial approval is still an approval as far as this module is concerned.</b> Trust knows the
/// difference between a fully verified residency and one verified on some documents; the
/// recommendation platform's question is only "may this member see verified-only content", and
/// answering it with Trust's nuance would be this module re-deciding somebody else's business rule.
/// </para>
/// <para>
/// The target country is recorded as a location signal. Somebody who verified their residency in a
/// country is, by that act, telling the platform where they are — often more reliably than the
/// registration form did.
/// </para>
/// </summary>
public sealed class VerificationApprovedConsumer
    : RecommendationEventConsumer<VerificationApprovedIntegrationEvent>
{
    public VerificationApprovedConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Trust;

    protected override Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var userId = UserId.From(integrationEvent.UserId);
        var drafts = new List<SignalDraft>(2)
        {
            Signal(userId, SignalKind.Verification, integrationEvent.OccurredAt, key: "verified")
        };

        if (!string.IsNullOrWhiteSpace(integrationEvent.TargetCountry))
        {
            drafts.Add(Signal(
                userId,
                SignalKind.Location,
                integrationEvent.OccurredAt,
                context: SignalContext.Create(integrationEvent.TargetCountry)));
        }

        return RecordAsync(drafts, cancellationToken);
    }
}
