using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>
/// A template became usable.
/// <para>
/// Never published outside the module. A template is this module's own reference data; announcing
/// its lifecycle on the platform bus would invite other modules to depend on wording they do not own.
/// It exists so the render cache can be invalidated inside the same transaction that published it.
/// </para>
/// </summary>
public sealed record TemplatePublishedDomainEvent : DomainEvent
{
    public required NotificationTemplateId TemplateId { get; init; }

    public required string TemplateKey { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
