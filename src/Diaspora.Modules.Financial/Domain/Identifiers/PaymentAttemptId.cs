using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One call to a gateway on behalf of a payment intent. Several per intent is normal.</summary>
public sealed record PaymentAttemptId(Guid Value) : StronglyTypedId(Value)
{
    public static PaymentAttemptId New() => new(Guid.NewGuid());

    public static PaymentAttemptId From(Guid value) => new(value);
}
