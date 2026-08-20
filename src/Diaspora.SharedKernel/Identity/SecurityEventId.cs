using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record SecurityEventId(Guid Value) : StronglyTypedId(Value)
{
    public static SecurityEventId New() => new(Guid.NewGuid());
    public static SecurityEventId From(Guid value) => new(value);
    public static readonly SecurityEventId Empty = new(Guid.Empty);
}
