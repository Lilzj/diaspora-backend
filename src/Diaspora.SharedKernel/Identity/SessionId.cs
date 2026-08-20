using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record SessionId(Guid Value) : StronglyTypedId(Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public static SessionId From(Guid value) => new(value);
}
