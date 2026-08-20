using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record ProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static ProfileId New() => new(Guid.NewGuid());
    public static ProfileId From(Guid value) => new(value);
}
