using Diaspora.Modules.Notifications.Domain.ValueObjects;

namespace Diaspora.Modules.Notifications.Extensibility.DeepLinks;

/// <summary>
/// Turns a route template and a set of values into a link a client can open.
/// <para>
/// <b>This module owns the link and never the destination.</b> The resolver substitutes, validates
/// the shape, and refuses anything that would leave the application — a scheme, a host, a traversal.
/// It does not ask whether a job with that identifier exists, because answering that would mean
/// asking eight modules a question none of them should be answering for a notification, and because
/// a message pointing at a deleted listing is better than no message at all.
/// </para>
/// <para>
/// A port rather than a static helper so a deployment can register its own route table — a web
/// client whose URLs differ from the mobile router's is one implementation, not a fork.
/// </para>
/// </summary>
public interface IDeepLinkResolver
{
    /// <summary>
    /// The finished link, or null when the template resolves to something unopenable.
    /// <para>
    /// Null rather than a failure: a rule pointing at a route the client has not shipped should cost
    /// the link, not the notification. An un-tappable message beats a silent one.
    /// </para>
    /// </summary>
    DeepLink? Resolve(
        string? routeTemplate,
        IReadOnlyDictionary<string, object?> values,
        string? entityType = null,
        string? entityId = null);

    /// <summary>Whether a route is one this platform will open. Used by template validation.</summary>
    bool IsKnownRoute(string? route);
}
