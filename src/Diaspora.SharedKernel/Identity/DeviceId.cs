using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record DeviceId(Guid Value) : StronglyTypedId(Value)
{
    public static DeviceId New() => new(Guid.NewGuid());
    public static DeviceId From(Guid value) => new(value);
    public static readonly DeviceId Empty = new(Guid.Empty);
}
