using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a member reacts to anything.
/// <para>
/// Carries the reaction key and its sentiment rather than an enum member, because the reaction
/// catalogue is data. A consumer that switched on a hardcoded reaction enum would silently stop
/// handling every reaction a later deployment adds.
/// </para>
/// </summary>
public sealed record ReactionAddedDomainEvent : DomainEvent
{
    public required ReactionId ReactionId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>The catalogue key, for example <c>helpful</c> or <c>confirmed</c>.</summary>
    public required string ReactionKey { get; init; }

    public required ReactionSentiment Sentiment { get; init; }

    /// <summary>The author of the content, when B6 owns it. Who a notification goes to.</summary>
    public Guid? ContentAuthorId { get; init; }
}
