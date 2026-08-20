using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>Every version under one template key, newest first.</summary>
[Authorize(Permission = NotificationPermissions.TemplatesRead)]
public sealed record GetNotificationTemplateQuery
    : IQuery<IReadOnlyList<NotificationTemplateResponse>>
{
    public required string Key { get; init; }
}
