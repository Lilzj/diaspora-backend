using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record ReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static ReviewId New() => new(Guid.NewGuid());
    public static ReviewId From(Guid value) => new(value);
}
