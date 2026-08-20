namespace Diaspora.SharedKernel.Primitives;

/// <summary>
/// The shape every message in the platform shares: an identity and a moment.
/// <para>
/// <b>This is a shape, not a channel.</b> It exists so that infrastructure which genuinely
/// treats all messages alike — a log line, a metric tag, a correlation trail — can be written
/// once. It is deliberately not a dispatch contract: nothing in the platform accepts an
/// <see cref="IMessage"/> and decides what to do with it, because that decision is exactly the
/// one the two hierarchies below it exist to keep apart.
/// </para>
/// <para>
/// <b>Why the root is this thin.</b> Before B1.2 the two hierarchies met at
/// <c>IDomainEvent</c> — an integration event <em>was</em> a domain event, so it satisfied every
/// domain-event API and the compiler had no opinion about which pipeline it entered. Half the
/// platform published through the domain dispatcher without anyone noticing, because nothing
/// could notice. Meeting at a member-only marker instead means the two hierarchies share the
/// two properties they actually have in common and nothing else: neither is substitutable for
/// the other, and a dispatcher or publisher typed on one cannot be handed the other.
/// </para>
/// <para>
/// The architecture tests forbid any dispatch or publish port from being typed on this
/// interface, so the seam cannot be quietly re-widened. See
/// <c>docs/platform/b1.2-messaging-architecture-guide.md</c>.
/// </para>
/// </summary>
public interface IMessage
{
    /// <summary>
    /// The message's own identity, stable across serialization and replay. Consumers
    /// deduplicate on this.
    /// </summary>
    Guid EventId { get; }

    /// <summary>When the fact this message reports became true.</summary>
    DateTimeOffset OccurredAt { get; }
}
