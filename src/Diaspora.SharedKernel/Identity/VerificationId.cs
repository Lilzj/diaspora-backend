using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record VerificationId(Guid Value) : StronglyTypedId(Value)
{
    public static VerificationId New() => new(Guid.NewGuid());
    public static VerificationId From(Guid value) => new(value);
}
