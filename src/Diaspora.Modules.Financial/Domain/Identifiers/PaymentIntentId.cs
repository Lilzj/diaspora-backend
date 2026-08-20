using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One attempt to move money in from outside the platform, through its whole lifecycle.</summary>
public sealed record PaymentIntentId(Guid Value) : StronglyTypedId(Value)
{
    public static PaymentIntentId New() => new(Guid.NewGuid());

    public static PaymentIntentId From(Guid value) => new(value);
}
