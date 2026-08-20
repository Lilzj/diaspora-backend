using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One immutable line in the ledger. Never updated, never deleted.</summary>
public sealed record LedgerEntryId(Guid Value) : StronglyTypedId(Value)
{
    public static LedgerEntryId New() => new(Guid.NewGuid());

    public static LedgerEntryId From(Guid value) => new(value);
}
