using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One member buying one product once.</summary>
public sealed record MonetizationPurchaseId(Guid Value) : StronglyTypedId(Value)
{
    public static MonetizationPurchaseId New() => new(Guid.NewGuid());

    public static MonetizationPurchaseId From(Guid value) => new(value);
}
