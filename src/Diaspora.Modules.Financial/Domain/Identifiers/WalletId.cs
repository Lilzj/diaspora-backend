using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One balance in one currency, belonging to one financial account. A projection of the ledger.</summary>
public sealed record WalletId(Guid Value) : StronglyTypedId(Value)
{
    public static WalletId New() => new(Guid.NewGuid());

    public static WalletId From(Guid value) => new(value);
}
