using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record MarketplaceListingId(Guid Value) : StronglyTypedId(Value)
{
    public static MarketplaceListingId New() => new(Guid.NewGuid());
    public static MarketplaceListingId From(Guid value) => new(value);
}
