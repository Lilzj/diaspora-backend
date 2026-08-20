using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers;

/// <summary>
/// The base every consumer of another module's contract derives from.
/// <para>
/// <b>It exists to make the consumers boring, and boring is the requirement.</b> A consumer's whole
/// job is to read a published payload and produce either a candidate projection or a signal — or
/// both. It must not rank, must not decide what a member sees, and must not name a strategy or a
/// policy. Giving every consumer the same two-verb shape means the architecture test that checks this
/// can be a simple one, and means a new consumer is twenty obviously-correct lines.
/// </para>
/// <para>
/// <b>Delivery is at-least-once and out of band.</b> This runs on a background thread, in its own
/// scope, after the producer's transaction committed — possibly minutes later, possibly twice.
/// Both verbs are idempotent by construction: a projection is an upsert, and a duplicate signal
/// changes an affinity by an amount that rounds away.
/// </para>
/// <para>
/// <b>A consumer that produces nothing is normal.</b> An event about a member the platform cannot
/// identify, or one that concerns nothing recommendable, does nothing at all.
/// </para>
/// </summary>
public abstract class RecommendationEventConsumer<TIntegrationEvent>
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    protected RecommendationEventConsumer(CandidateProjector projector, ISignalStore signals)
    {
        Projector = projector;
        Signals = signals;
    }

    /// <summary>Which module's contract this consumes. Recorded as the signal's provenance.</summary>
    protected abstract SignalSource Source { get; }

    /// <summary>The projector. Use it to create, refresh or withdraw a candidate.</summary>
    protected CandidateProjector Projector { get; }

    /// <summary>The signal store. The only way to record an observation — see <see cref="ISignalStore"/>.</summary>
    protected ISignalStore Signals { get; }

    /// <summary>
    /// The module name recorded on a projected candidate.
    /// <para>
    /// Derived from the source rather than declared separately, so the two can never disagree about
    /// which vertical a row came from.
    /// </para>
    /// </summary>
    protected string SourceModule => Source.ToString();

    /// <summary>
    /// The namespace-qualified contract name.
    /// <para>
    /// <b>Qualified, not short, and that is not tidiness.</b> Jobs and Marketplace both publish an
    /// <c>OfferAcceptedIntegrationEvent</c>; Marketplace and Housing both publish a
    /// <c>ReservationCancelledIntegrationEvent</c>. A signal attributed by short name would credit
    /// one to the other in every audit. Taken from the CLR type rather than a literal, so renaming a
    /// published contract cannot silently detach a signal from its origin.
    /// </para>
    /// </summary>
    protected static string EventName => typeof(TIntegrationEvent).FullName!;

    /// <summary>Builds a signal draft with the source and the contract name filled in.</summary>
    protected SignalDraft Signal(
        UserId userId,
        SignalKind kind,
        DateTimeOffset occurredAt,
        RecommendationTargetType? targetType = null,
        Guid? targetId = null,
        string? key = null,
        InterestKind? keyKind = null,
        SignalContext? context = null,
        decimal? weight = null) =>
        new()
        {
            UserId = userId,
            Kind = kind,
            Source = Source,
            TargetType = targetType,
            TargetId = targetId,
            Key = key,
            KeyKind = keyKind,
            Context = context,
            Weight = weight,
            SourceEventName = EventName,
            OccurredAt = occurredAt
        };

    /// <summary>Records one draft.</summary>
    protected Task RecordAsync(SignalDraft draft, CancellationToken cancellationToken) =>
        Signals.RecordAsync(draft, cancellationToken);

    /// <summary>Records several drafts, raising one event for the batch.</summary>
    protected Task RecordAsync(IReadOnlyList<SignalDraft> drafts, CancellationToken cancellationToken) =>
        Signals.RecordManyAsync(drafts, cancellationToken);

    /// <summary>
    /// Turns a list of tag-like strings into candidate attributes.
    /// <para>
    /// Here rather than in each consumer because every vertical publishes its tags as a list of
    /// strings and every consumer would otherwise write the same three lines slightly differently.
    /// </para>
    /// </summary>
    protected static IReadOnlyList<(InterestKind Kind, string Key)> Attributes(
        InterestKind kind, IEnumerable<string>? keys) =>
        (keys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => (kind, key.Trim()))
            .ToList();
}
