using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record GuideId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideId New() => new(Guid.NewGuid());
    public static GuideId From(Guid value) => new(value);
}
