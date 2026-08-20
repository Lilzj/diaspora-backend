using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One thing that can be bought here, as reference data.</summary>
public sealed record MonetizationProductId(Guid Value) : StronglyTypedId(Value)
{
    public static MonetizationProductId New() => new(Guid.NewGuid());

    public static MonetizationProductId From(Guid value) => new(value);
}
