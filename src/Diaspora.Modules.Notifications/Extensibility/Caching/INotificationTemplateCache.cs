namespace Diaspora.Modules.Notifications.Extensibility.Caching;

/// <summary>
/// Remembers which template row is published under a key.
/// <para>
/// <b>A module-owned port rather than the platform's <c>ICacheService</c>, and that is a deliberate
/// refusal to claim something this module does not own.</b> The kernel declares <c>ICacheService</c>
/// and nothing in the platform implements it; registering an implementation here would make the
/// Communication Platform the de facto owner of platform-wide caching, which is a decision for
/// whoever builds that — not a side effect of wanting to avoid a database round trip per
/// notification.
/// </para>
/// <para>
/// So the seam is narrow: one key space, one value type, one expiry. The shipped implementation is
/// in-memory and correct for a single instance; a Redis-backed one is a registration swap, and so is
/// an adapter over <c>ICacheService</c> the day it grows an implementation.
/// </para>
/// <para>
/// <b>It caches the identifier, never the aggregate.</b> An EF entity in a shared cache would be
/// handed to a second request's change tracker, which is a class of bug worth far more than the
/// round trip it saves.
/// </para>
/// </summary>
public interface INotificationTemplateCache
{
    /// <summary>The published template's identifier for a key, or null when nothing is remembered.</summary>
    Guid? Find(string templateKey);

    /// <summary>Remembers a key's published template for the given lifetime.</summary>
    void Remember(string templateKey, Guid templateId, TimeSpan lifetime);

    /// <summary>Forgets a key. Called when a template is published or retired.</summary>
    void Forget(string templateKey);
}
