using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Edits a template.
/// <para>
/// Against a draft it edits in place. Against a published template it opens the next version and
/// edits that — which is what makes a wording change reversible without a deployment, and what stops
/// a typo reaching two hundred thousand people because somebody saved.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.TemplatesManage)]
[Transactional(Reason = "Edits or versions the template and its collections together.")]
[Auditable]
public sealed record UpdateNotificationTemplateCommand : ICommand<NotificationTemplateResponse>
{
    public required Guid TemplateId { get; init; }

    public required UpdateNotificationTemplateRequest Request { get; init; }
}
