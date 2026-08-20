using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a member's subscription to a feed.</summary>
public sealed record FeedSubscriptionId(Guid Value) : StronglyTypedId(Value)
{
    public static FeedSubscriptionId New() => new(Guid.NewGuid());

    public static FeedSubscriptionId From(Guid value) => new(value);
}
