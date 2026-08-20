using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Extensibility.Caching;
using Diaspora.Modules.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// Resolves published templates, through the module's own cache.
/// <para>
/// <b>The one read in this module that genuinely needs caching.</b> Every notification resolves a
/// template, so a busy hour is tens of thousands of identical lookups of a handful of rows that
/// change when somebody presses publish — perhaps weekly. Caching it turns the dispatcher's
/// per-message database work from three queries into two.
/// </para>
/// <para>
/// <b>Cached by key, invalidated by publication.</b> The template aggregate raises
/// <c>TemplatePublishedDomainEvent</c> and <c>TemplateDeactivatedDomainEvent</c>, and the handlers
/// for both evict — inside the transaction that published, so the window in which a stale template
/// could be rendered is the transaction itself. The time-to-live is a backstop, because a cache whose
/// only invalidation is an in-process event goes permanently stale the first time it runs on two
/// machines.
/// </para>
/// <para>
/// It caches the <em>identifier</em> and re-reads the aggregate. An EF entity in a shared cache
/// would be handed to a second request's change tracker, which is a class of bug worth more than the
/// round trip it saves.
/// </para>
/// </summary>
public sealed class NotificationTemplateCatalog
{
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateCache _cache;
    private readonly IOptions<NotificationOptions> _options;

    public NotificationTemplateCatalog(
        INotificationTemplateRepository templates,
        INotificationTemplateCache cache,
        IOptions<NotificationOptions> options)
    {
        _templates = templates;
        _cache = cache;
        _options = options;
    }

    /// <summary>The usable template under a key, or null when nothing is published under it.</summary>
    public async Task<NotificationTemplate?> FindPublishedAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = key.Trim().ToLowerInvariant();

        if (_cache.Find(normalized) is { } identifier && identifier != Guid.Empty)
        {
            var cached = await _templates.GetAsync(
                NotificationTemplateId.From(identifier), cancellationToken);

            // A cached identifier for a template that has since been retired must not be served.
            // Falling through re-reads and re-caches, which is also how a deactivation with a missed
            // eviction eventually corrects itself.
            if (cached is { IsUsable: true })
            {
                return cached;
            }
        }

        var template = await _templates.FindPublishedAsync(normalized, cancellationToken);

        if (template is not null)
        {
            _cache.Remember(
                normalized,
                template.Id.Value,
                TimeSpan.FromSeconds(_options.Value.TemplateCacheSeconds));
        }

        return template;
    }

    /// <summary>Drops a key from the cache. Called when a template is published or retired.</summary>
    public void Evict(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _cache.Forget(key.Trim().ToLowerInvariant());
    }
}
