using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A template was retired: drop it from the render cache.
/// <para>
/// The more important of the two evictions. A stale <em>published</em> template renders the previous
/// wording for a few minutes; a stale <em>retired</em> one keeps sending a message somebody
/// deliberately stopped.
/// </para>
/// </summary>
public sealed class TemplateDeactivatedEventHandler
    : DomainEventHandler<TemplateDeactivatedDomainEvent>
{
    private readonly NotificationTemplateCatalog _catalog;

    public TemplateDeactivatedEventHandler(NotificationTemplateCatalog catalog) => _catalog = catalog;

    protected override Task HandleAsync(
        TemplateDeactivatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _catalog.Evict(domainEvent.TemplateKey);

        return Task.CompletedTask;
    }
}
