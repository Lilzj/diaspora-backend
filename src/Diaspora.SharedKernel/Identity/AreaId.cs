using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record AreaId(Guid Value) : StronglyTypedId(Value)
{
    public static AreaId New() => new(Guid.NewGuid());
    public static AreaId From(Guid value) => new(value);
}
