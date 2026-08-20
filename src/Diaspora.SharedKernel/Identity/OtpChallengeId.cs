using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record OtpChallengeId(Guid Value) : StronglyTypedId(Value)
{
    public static OtpChallengeId New() => new(Guid.NewGuid());
    public static OtpChallengeId From(Guid value) => new(value);
    public static readonly OtpChallengeId Empty = new(Guid.Empty);
}
