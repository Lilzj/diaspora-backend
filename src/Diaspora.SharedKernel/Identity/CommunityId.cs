using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record CommunityId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityId New() => new(Guid.NewGuid());
    public static CommunityId From(Guid value) => new(value);
}
