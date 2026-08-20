namespace Diaspora.Contracts.Events;

/// <summary>
/// Base record for integration events.
/// <para>
/// <b>It derives from nothing.</b> Until B1.2 it derived from <c>DomainEvent</c>, which is how
/// every published contract in the platform came to satisfy the domain event API — and how
/// Trust and Community came to publish 29 of the platform's 42 contracts straight through
/// <c>IDomainEventDispatcher</c>, bypassing the port that exists to stop exactly that. No test
/// caught it, because nothing was broken; the events were delivered, in-process, precisely as
/// documented. What was missing was any way for the compiler to have an opinion.
/// </para>
/// <para>
/// <b>The two properties below are the same two it inherited before</b> — same names, same
/// types, same defaults, declared here instead of two levels up. That is deliberate and it is
/// what makes this a source-only migration: the serialized payload is byte-identical, because
/// <c>System.Text.Json</c> writes a derived type's own properties first and then walks up, and
/// this record still sits directly beneath every concrete event. Every row already in an outbox
/// table replays unchanged, and every consumer parses what it parsed yesterday. See
/// <c>docs/platform/b1.2-breaking-change-report.md</c>.
/// </para>
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// The published identity of this event. Consumers deduplicate on it, and a replay must
    /// reproduce it exactly — which is why it is part of the serialized payload rather than
    /// envelope metadata.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>When the fact happened, not when it was delivered.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
