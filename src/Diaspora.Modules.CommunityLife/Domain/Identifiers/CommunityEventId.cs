using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>
/// A community event's identity.
/// <para>
/// Named <c>CommunityEventId</c> rather than <c>EventId</c> on purpose. "Event" already means
/// something in this codebase — a domain event, an integration event, an outbox row — and a type
/// called <c>EventId</c> sitting beside <c>IDomainEvent</c> would be read wrongly by everyone who
/// arrived after it. B9 dodged the same collision when it named <c>JobPostingId</c>.
/// </para>
/// </summary>
public sealed record CommunityEventId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityEventId New() => new(Guid.NewGuid());

    public static CommunityEventId From(Guid value) => new(value);
}
