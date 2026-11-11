using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Retires a template.
/// <para>
/// Messages already sent from it are untouched — they carry their own rendered words, which is the
/// whole reason rendering happens once. What stops is any new message using it, and a rule that
/// still names it starts logging loudly.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.TemplatesPublish)]
[Transactional(Reason = "Retires the template and evicts the render cache together.")]
[Auditable]
public sealed record DeactivateNotificationTemplateCommand : ICommand<NotificationTemplateResponse>
{
    public required Guid TemplateId { get; init; }
}
