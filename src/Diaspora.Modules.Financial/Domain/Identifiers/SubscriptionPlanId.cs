using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One recurring plan in the catalogue.</summary>
public sealed record SubscriptionPlanId(Guid Value) : StronglyTypedId(Value)
{
    public static SubscriptionPlanId New() => new(Guid.NewGuid());

    public static SubscriptionPlanId From(Guid value) => new(value);
}
