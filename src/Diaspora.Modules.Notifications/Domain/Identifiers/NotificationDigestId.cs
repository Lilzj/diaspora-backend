using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>One assembled summary for one member over one window.</summary>
public sealed record NotificationDigestId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationDigestId New() => new(Guid.NewGuid());

    public static NotificationDigestId From(Guid value) => new(value);
}
