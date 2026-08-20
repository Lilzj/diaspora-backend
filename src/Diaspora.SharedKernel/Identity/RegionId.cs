using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record RegionId(Guid Value) : StronglyTypedId(Value)
{
    public static RegionId New() => new(Guid.NewGuid());
    public static RegionId From(Guid value) => new(value);
}
