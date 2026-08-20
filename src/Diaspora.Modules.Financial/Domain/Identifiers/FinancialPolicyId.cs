using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One version of one configurable financial rule. Versions are rows, never edits.</summary>
public sealed record FinancialPolicyId(Guid Value) : StronglyTypedId(Value)
{
    public static FinancialPolicyId New() => new(Guid.NewGuid());

    public static FinancialPolicyId From(Guid value) => new(value);
}
