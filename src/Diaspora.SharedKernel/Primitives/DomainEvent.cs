namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// Base record for domain events. A record for immutability and value equality.
/// <para>
/// <b>Nothing derives from this except domain events.</b> Until B1.2, <c>IntegrationEvent</c>
/// did — which made every published contract in the platform a domain event as far as the
/// compiler was concerned, and made the domain dispatcher accept both. The two base records
/// are now independent, and neither inherits from the other; they share
/// <see cref="IMessage"/> and nothing more. See
/// <c>docs/platform/b1.2-messaging-architecture-guide.md</c>.
/// </para>
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
