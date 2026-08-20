using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>One authored message shape, across every language and channel it has.</summary>
public sealed record NotificationTemplateId(Guid Value) : StronglyTypedId(Value)
{
    public static NotificationTemplateId New() => new(Guid.NewGuid());

    public static NotificationTemplateId From(Guid value) => new(value);
}
