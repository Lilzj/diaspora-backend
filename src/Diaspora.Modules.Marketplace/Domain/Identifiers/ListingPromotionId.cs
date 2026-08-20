using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A promotion's identity.</summary>
public sealed record ListingPromotionId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingPromotionId New() => new(Guid.NewGuid());

    public static ListingPromotionId From(Guid value) => new(value);
}
