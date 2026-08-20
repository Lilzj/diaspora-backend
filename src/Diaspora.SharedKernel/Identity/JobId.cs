using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record JobId(Guid Value) : StronglyTypedId(Value)
{
    public static JobId New() => new(Guid.NewGuid());
    public static JobId From(Guid value) => new(value);
}
