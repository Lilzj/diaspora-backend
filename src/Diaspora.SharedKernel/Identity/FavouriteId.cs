using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record FavouriteId(Guid Value) : StronglyTypedId(Value)
{
    public static FavouriteId New() => new(Guid.NewGuid());
    public static FavouriteId From(Guid value) => new(value);
}
