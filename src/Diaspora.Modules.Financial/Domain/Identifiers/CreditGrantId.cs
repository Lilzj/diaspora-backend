using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>Platform credit granted to a wallet, with a reason and an optional expiry.</summary>
public sealed record CreditGrantId(Guid Value) : StronglyTypedId(Value)
{
    public static CreditGrantId New() => new(Guid.NewGuid());

    public static CreditGrantId From(Guid value) => new(value);
}
