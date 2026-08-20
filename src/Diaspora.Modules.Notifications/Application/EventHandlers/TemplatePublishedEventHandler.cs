using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A template became usable: drop it from the render cache.
/// <para>
/// Nothing is published. A template is this module's own reference data, and announcing its
/// lifecycle would invite other modules to depend on wording they do not own.
/// </para>
/// <para>
/// The eviction happens inside the transaction that published, so the window in which a stale
/// template could be rendered is the transaction itself.
/// </para>
/// </summary>
public sealed class TemplatePublishedEventHandler : DomainEventHandler<TemplatePublishedDomainEvent>
{
    private readonly NotificationTemplateCatalog _catalog;

    public TemplatePublishedEventHandler(NotificationTemplateCatalog catalog) => _catalog = catalog;

    protected override Task HandleAsync(
        TemplatePublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _catalog.Evict(domainEvent.TemplateKey);

        return Task.CompletedTask;
    }
}
