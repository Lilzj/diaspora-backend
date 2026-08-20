using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Where a notification sends the member.
/// <para>
/// <b>This module owns the link and never the destination.</b> The route is a client path — the
/// same string the mobile router already navigates on — and validating that something exists at the
/// other end would mean asking eight modules a question none of them should be answering for a
/// notification. What is validated is the shape: a relative path, no scheme, no host, no query
/// smuggling an absolute URL. That is the whole of deep-link security here, and it is enough,
/// because a route that names nothing simply lands on the client's not-found screen while a route
/// that names an external host would be a phishing vector wearing the platform's badge.
/// </para>
/// <para>
/// <see cref="Route"/> is stored already substituted. Keeping the template and its parameters
/// separate would mean re-substituting on read, and re-substitution is how a message written about
/// listing A ends up pointing at listing B after a template edit.
/// </para>
/// </summary>
public sealed class DeepLink : ValueObject
{
    private DeepLink(string route, string? entityType, string? entityId)
    {
        Route = route;
        EntityType = entityType;
        EntityId = entityId;
    }

    private DeepLink() { }

    /// <summary>The client route, already substituted. Always starts with a single forward slash.</summary>
    public string Route { get; private set; } = "/";

    /// <summary>What kind of thing is at the end of it — <c>job</c>, <c>listing</c>, <c>guide</c>.</summary>
    public string? EntityType { get; private set; }

    /// <summary>Its identifier, held as a string because not every destination is keyed by a Guid.</summary>
    public string? EntityId { get; private set; }

    /// <summary>
    /// Builds a link, or returns null when the route is not one this platform will open.
    /// <para>
    /// Null rather than throwing: a rule referencing a route the client has not shipped yet is a
    /// configuration mistake that should cost the link, not the notification. A member is far better
    /// served by an un-tappable message than by no message.
    /// </para>
    /// </summary>
    public static DeepLink? TryCreate(string? route, string? entityType = null, string? entityId = null)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var trimmed = route.Trim();

        if (trimmed.Length > NotificationLimits.MaxDeepLinkLength)
        {
            return null;
        }

        // A scheme, a protocol-relative prefix or a backslash all mean "somewhere that is not this
        // application", and every one of them has been used to make an in-app link open a browser.
        if (trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        if (!trimmed.StartsWith('/'))
        {
            return null;
        }

        return new DeepLink(
            trimmed,
            Clip(entityType),
            Clip(entityId));
    }

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= NotificationLimits.MaxSubjectIdLength
            ? trimmed
            : trimmed[..NotificationLimits.MaxSubjectIdLength];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Route;
        yield return EntityType;
        yield return EntityId;
    }
}
