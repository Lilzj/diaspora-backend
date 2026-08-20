using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A coupon, campaign discount or referral reward, as reference data.</summary>
public sealed record PromotionId(Guid Value) : StronglyTypedId(Value)
{
    public static PromotionId New() => new(Guid.NewGuid());

    public static PromotionId From(Guid value) => new(value);
}
