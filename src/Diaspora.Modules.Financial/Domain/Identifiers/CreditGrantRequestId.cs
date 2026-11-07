using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A credit grant waiting for a second Admin.</summary>
public sealed record CreditGrantRequestId(Guid Value) : StronglyTypedId(Value)
{
    public static CreditGrantRequestId New() => new(Guid.NewGuid());

    public static CreditGrantRequestId From(Guid value) => new(value);
}
