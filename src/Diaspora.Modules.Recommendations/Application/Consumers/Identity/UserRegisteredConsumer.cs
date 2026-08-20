using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Identity;

/// <summary>
/// Somebody joined the platform.
/// <para>
/// <b>The single most valuable event this module consumes, and it arrives before the member has done
/// anything at all.</b> Registration carries a country of residence, a nationality and a language —
/// three facts that between them narrow the platform's entire corpus to the part that could be
/// useful to this person. Without them the first feed would be globally popular content in a
/// language they may not read, which is the worst first impression the product can make.
/// </para>
/// <para>
/// <b>Nationality is recorded as a country affinity, not as a location.</b> Somebody Nigerian living
/// in Mauritius is genuinely interested in both, and the distinction matters: the location strategy
/// scores where they are at its ceiling and where they are from at a discount, which is the right
/// ordering for a housing search and, arguably, the wrong one for a community. It is a discount
/// rather than an exclusion for exactly that reason.
/// </para>
/// </summary>
public sealed class UserRegisteredConsumer
    : RecommendationEventConsumer<UserRegisteredIntegrationEvent>
{
    public UserRegisteredConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Identity;

    protected override Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var userId = UserId.From(integrationEvent.UserId);
        var drafts = new List<SignalDraft>(4);

        // Where they are. The anchor the whole cold-start ladder is built on.
        if (!string.IsNullOrWhiteSpace(integrationEvent.CountryOfResidence))
        {
            drafts.Add(Signal(
                userId,
                SignalKind.Location,
                integrationEvent.OccurredAt,
                context: SignalContext.Create(integrationEvent.CountryOfResidence)));
        }

        // Where they are from. An interest, not a location — see the class summary.
        if (!string.IsNullOrWhiteSpace(integrationEvent.Nationality) &&
            !string.Equals(
                integrationEvent.Nationality,
                integrationEvent.CountryOfResidence,
                StringComparison.OrdinalIgnoreCase))
        {
            drafts.Add(Signal(
                userId,
                SignalKind.Interest,
                integrationEvent.OccurredAt,
                key: integrationEvent.Nationality,
                keyKind: InterestKind.Tag));
        }

        if (!string.IsNullOrWhiteSpace(integrationEvent.PreferredLanguage))
        {
            drafts.Add(Signal(
                userId,
                SignalKind.Language,
                integrationEvent.OccurredAt,
                key: integrationEvent.PreferredLanguage,
                keyKind: InterestKind.Language));
        }

        // Registration is not the end of onboarding. Recorded explicitly so the onboarding policy
        // has something to read for a member who never gets any further.
        drafts.Add(Signal(
            userId, SignalKind.Onboarding, integrationEvent.OccurredAt, key: "incomplete"));

        return RecordAsync(drafts, cancellationToken);
    }
}
