using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One crossing from points into money.</summary>
public sealed record RewardConversionId(Guid Value) : StronglyTypedId(Value)
{
    public static RewardConversionId New() => new(Guid.NewGuid());

    public static RewardConversionId From(Guid value) => new(value);
}
