using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record SuspensionId(Guid Value) : StronglyTypedId(Value)
{
    public static SuspensionId New() => new(Guid.NewGuid());
    public static SuspensionId From(Guid value) => new(value);
}
