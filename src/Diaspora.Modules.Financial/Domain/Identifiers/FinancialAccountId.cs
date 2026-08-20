using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A member's, organization's or provider's financial identity. One per owner, created once.</summary>
public sealed record FinancialAccountId(Guid Value) : StronglyTypedId(Value)
{
    public static FinancialAccountId New() => new(Guid.NewGuid());

    public static FinancialAccountId From(Guid value) => new(value);
}
