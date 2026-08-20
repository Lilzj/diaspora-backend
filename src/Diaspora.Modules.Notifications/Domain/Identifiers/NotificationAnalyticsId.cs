using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>One day's measured record for one category and channel pair.</summary>
public sealed record NotificationAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationAnalyticsId New() => new(Guid.NewGuid());

    public static NotificationAnalyticsId From(Guid value) => new(value);
}
