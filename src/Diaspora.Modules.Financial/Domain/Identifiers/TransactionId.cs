using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>The unit of work in the ledger: every entry it produced balances to zero.</summary>
public sealed record TransactionId(Guid Value) : StronglyTypedId(Value)
{
    public static TransactionId New() => new(Guid.NewGuid());

    public static TransactionId From(Guid value) => new(value);
}
