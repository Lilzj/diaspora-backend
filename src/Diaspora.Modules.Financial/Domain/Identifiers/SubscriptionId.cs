using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One member's or organization's subscription to one plan.</summary>
public sealed record SubscriptionId(Guid Value) : StronglyTypedId(Value)
{
    public static SubscriptionId New() => new(Guid.NewGuid());

    public static SubscriptionId From(Guid value) => new(value);
}
