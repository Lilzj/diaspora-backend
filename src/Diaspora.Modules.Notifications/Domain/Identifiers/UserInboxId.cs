using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>One member's inbox counters. One row per member, forever.</summary>
public sealed record UserInboxId(Guid Value) : StronglyTypedId(Value)
{
    public static UserInboxId New() => new(Guid.NewGuid());

    public static UserInboxId From(Guid value) => new(value);
}
