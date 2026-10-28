using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A staff grant waiting for a second Admin.</summary>
public sealed record RewardGrantRequestId(Guid Value) : StronglyTypedId(Value)
{
    public static RewardGrantRequestId New() => new(Guid.NewGuid());

    public static RewardGrantRequestId From(Guid value) => new(value);
}
