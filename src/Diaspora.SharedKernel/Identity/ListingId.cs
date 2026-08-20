using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record ListingId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingId New() => new(Guid.NewGuid());
    public static ListingId From(Guid value) => new(value);
}
