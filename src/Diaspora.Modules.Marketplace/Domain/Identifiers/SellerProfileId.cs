using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A seller profile's identity.</summary>
public sealed record SellerProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static SellerProfileId New() => new(Guid.NewGuid());

    public static SellerProfileId From(Guid value) => new(value);
}
