using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One rolled-up revenue measurement over one period and one dimension.</summary>
public sealed record RevenueRecordId(Guid Value) : StronglyTypedId(Value)
{
    public static RevenueRecordId New() => new(Guid.NewGuid());

    public static RevenueRecordId From(Guid value) => new(value);
}
