using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>Money returned for a captured payment, in whole or in part.</summary>
public sealed record RefundId(Guid Value) : StronglyTypedId(Value)
{
    public static RefundId New() => new(Guid.NewGuid());

    public static RefundId From(Guid value) => new(value);
}
