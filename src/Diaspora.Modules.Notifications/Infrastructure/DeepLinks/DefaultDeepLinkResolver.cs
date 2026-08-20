using System.Globalization;
using System.Text;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.DeepLinks;

namespace Diaspora.Modules.Notifications.Infrastructure.DeepLinks;

/// <summary>
/// Substitutes a route template and refuses anything that would leave the application.
/// <para>
/// <b>Two checks, and each stops a different thing.</b> <see cref="DeepLink.TryCreate"/> refuses
/// schemes, protocol-relative prefixes, backslashes and traversal — the shapes that turn an in-app
/// link into a browser opening somewhere else, which is phishing wearing the platform's badge. The
/// route table then refuses anything the client has no screen for, so a rule pointing at a route
/// that has not shipped costs the link rather than landing a member on nothing.
/// </para>
/// <para>
/// Substituted values are percent-encoded for the path, which matters because an identifier is not
/// always a Guid: a slug can contain a space, and a raw one would produce a route that half the
/// clients truncate.
/// </para>
/// </summary>
public sealed class DefaultDeepLinkResolver : IDeepLinkResolver
{
    public DeepLink? Resolve(
        string? routeTemplate,
        IReadOnlyDictionary<string, object?> values,
        string? entityType = null,
        string? entityId = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrWhiteSpace(routeTemplate))
        {
            return null;
        }

        var route = Substitute(routeTemplate, values);

        if (!IsKnownRoute(route))
        {
            return null;
        }

        return DeepLink.TryCreate(route, entityType, entityId);
    }

    public bool IsKnownRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || !route.StartsWith('/'))
        {
            return false;
        }

        foreach (var prefix in DeepLinkRouteTable.KnownPrefixes)
        {
            if (string.Equals(route, prefix, StringComparison.Ordinal))
            {
                return true;
            }

            if (route.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One pass, encoding each value.
    /// <para>
    /// A missing value collapses to an empty segment rather than leaving the literal token in the
    /// path — <c>/jobs/</c> is a list screen, whereas <c>/jobs/{jobId}</c> is a 404 with a brace in
    /// it. Neither is what was wanted; only one of them is embarrassing.
    /// </para>
    /// </summary>
    private static string Substitute(string template, IReadOnlyDictionary<string, object?> values)
    {
        var output = new StringBuilder(template.Length + 32);
        var token = new StringBuilder(32);
        var inToken = false;

        foreach (var character in template)
        {
            if (character == '{')
            {
                inToken = true;
                token.Clear();
                continue;
            }

            if (character == '}' && inToken)
            {
                var name = token.ToString().Trim().ToLowerInvariant();

                if (values.TryGetValue(name, out var value) && value is not null)
                {
                    output.Append(Uri.EscapeDataString(
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
                }

                inToken = false;
                token.Clear();
                continue;
            }

            if (inToken)
            {
                token.Append(character);
            }
            else
            {
                output.Append(character);
            }
        }

        return output.ToString();
    }
}
