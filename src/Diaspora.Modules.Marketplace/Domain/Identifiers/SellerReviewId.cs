using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A seller review's identity.</summary>
public sealed record SellerReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static SellerReviewId New() => new(Guid.NewGuid());

    public static SellerReviewId From(Guid value) => new(value);
}
