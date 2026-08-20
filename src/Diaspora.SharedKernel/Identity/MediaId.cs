using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record MediaId(Guid Value) : StronglyTypedId(Value)
{
    public static MediaId New() => new(Guid.NewGuid());
    public static MediaId From(Guid value) => new(value);
}
