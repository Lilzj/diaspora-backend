using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Makes a template usable.
/// <para>
/// A separate permission from authoring, deliberately. Writing a message the platform will send in
/// its own name to hundreds of thousands of people and deciding it is ready are two decisions, and
/// on a platform this one's members trust, they should be able to be two people.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.TemplatesPublish)]
[Transactional(Reason = "Publishes the template and evicts the render cache together.")]
[Auditable]
public sealed record PublishNotificationTemplateCommand : ICommand<NotificationTemplateResponse>
{
    public required Guid TemplateId { get; init; }
}
