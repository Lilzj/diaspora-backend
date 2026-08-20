using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One use of one promotion by one subject.</summary>
public sealed record PromotionRedemptionId(Guid Value) : StronglyTypedId(Value)
{
    public static PromotionRedemptionId New() => new(Guid.NewGuid());

    public static PromotionRedemptionId From(Guid value) => new(value);
}
