using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>Creates a draft template. Staff only — a template speaks in the platform's name.</summary>
[RequirePermission(NotificationPermissions.TemplatesManage)]
[Transactional(Reason = "Creates the template and its localizations together.")]
[Auditable]
public sealed record CreateNotificationTemplateCommand : ICommand<NotificationTemplateResponse>
{
    public required CreateNotificationTemplateRequest Request { get; init; }
}
