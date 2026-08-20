using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>A row in the configurable notification-category catalogue.</summary>
public sealed record NotificationCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationCategoryId New() => new(Guid.NewGuid());

    public static NotificationCategoryId From(Guid value) => new(value);
}
