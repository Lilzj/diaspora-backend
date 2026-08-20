using System.Collections.Concurrent;
using Diaspora.Modules.Notifications.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Notifications.Infrastructure.Caching;

/// <summary>
/// The shipped template cache: a concurrent dictionary with an expiry per entry.
/// <para>
/// <b>Correct for a single instance and approximate across several</b> — each instance caches
/// independently, so a publication evicts on the instance that served it and the others carry the
/// previous template until their entry expires. That is why the time-to-live exists as well as the
/// eviction: a cache whose only invalidation is an in-process event goes permanently stale the first
/// time it runs on two machines.
/// </para>
/// <para>
/// A singleton, and safe as one: it holds a concurrent dictionary and no scoped dependency. It is
/// also the one component here whose whole value is that it outlives a request.
/// </para>
/// </summary>
public sealed class InMemoryNotificationTemplateCache : INotificationTemplateCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public InMemoryNotificationTemplateCache(IClock clock) => _clock = clock;

    public Guid? Find(string templateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        if (!_entries.TryGetValue(templateKey, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt > _clock.UtcNow)
        {
            return entry.TemplateId;
        }

        // Removed on read rather than by a sweep. The key space is the number of templates the
        // platform ships, and a background thread to tidy thirty entries would cost more than it
        // saves.
        _entries.TryRemove(templateKey, out _);

        return null;
    }

    public void Remember(string templateKey, Guid templateId, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        _entries[templateKey] = new Entry(templateId, _clock.UtcNow.Add(lifetime));
    }

    public void Forget(string templateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);

        _entries.TryRemove(templateKey, out _);
    }

    private readonly record struct Entry(Guid TemplateId, DateTimeOffset ExpiresAt);
}
