using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One request to take money off the platform.</summary>
public sealed record WithdrawalId(Guid Value) : StronglyTypedId(Value)
{
    public static WithdrawalId New() => new(Guid.NewGuid());

    public static WithdrawalId From(Guid value) => new(value);
}
