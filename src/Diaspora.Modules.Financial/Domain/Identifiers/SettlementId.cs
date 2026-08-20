using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A future payout run. Declared so the seam has a name; nothing settles today.</summary>
public sealed record SettlementId(Guid Value) : StronglyTypedId(Value)
{
    public static SettlementId New() => new(Guid.NewGuid());

    public static SettlementId From(Guid value) => new(value);
}
