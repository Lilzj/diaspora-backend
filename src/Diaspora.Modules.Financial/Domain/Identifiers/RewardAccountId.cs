using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One member's points balance. Points are not money and this is not a wallet.</summary>
public sealed record RewardAccountId(Guid Value) : StronglyTypedId(Value)
{
    public static RewardAccountId New() => new(Guid.NewGuid());

    public static RewardAccountId From(Guid value) => new(value);
}
