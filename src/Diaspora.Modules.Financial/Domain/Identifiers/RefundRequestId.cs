using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A member asking for money back. Not a refund.</summary>
public sealed record RefundRequestId(Guid Value) : StronglyTypedId(Value)
{
    public static RefundRequestId New() => new(Guid.NewGuid());

    public static RefundRequestId From(Guid value) => new(value);
}
