using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record CityId(Guid Value) : StronglyTypedId(Value)
{
    public static CityId New() => new(Guid.NewGuid());
    public static CityId From(Guid value) => new(value);
}
