using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>One member's complete statement about how they want to be contacted.</summary>
public sealed record NotificationPreferenceId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationPreferenceId New() => new(Guid.NewGuid());

    public static NotificationPreferenceId From(Guid value) => new(value);
}
