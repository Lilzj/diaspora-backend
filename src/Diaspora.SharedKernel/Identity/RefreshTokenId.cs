using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record RefreshTokenId(Guid Value) : StronglyTypedId(Value)
{
    public static RefreshTokenId New() => new(Guid.NewGuid());
    public static RefreshTokenId From(Guid value) => new(value);
    public static readonly RefreshTokenId Empty = new(Guid.Empty);
}
