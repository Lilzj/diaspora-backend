using System.Net;
using System.Text;
using System.Text.Json;
using Diaspora.Email;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.DeepLinks;

namespace Diaspora.Api.Hosting;

/// <summary>
/// <c>GET /open/{route}</c>: the page behind every "Open in Diaspora" button in an email.
/// <para>
/// <b>Why it exists.</b> Until 20 Oct 2026 an email's button was <c>{AppBaseUrl}{route}</c>, for
/// example <c>https://diaspora-api.fly.dev/profile/trust-center</c>. The API serves no page there,
/// so every tap on a phone landed on a 404. The button is now <c>{AppBaseUrl}/open/{route}</c>
/// (<see cref="AppOpenLink"/>). With the app installed and its App Link verified, Android opens the
/// screen directly and this page is never fetched; everywhere else this page tries
/// <c>diaspora://{route}</c> at once and shows a button for when the automatic hand-off does not fire.
/// </para>
/// <para>
/// <b>Not an open redirect.</b> The destination is always the app's own scheme, and the route must
/// pass the same two checks a notification's deep link does: <see cref="DeepLink.TryCreate"/>
/// (no scheme, no <c>//</c>, no backslash, no traversal) and the client's route table
/// (<see cref="IDeepLinkResolver.IsKnownRoute"/>). Anything else is a plain 404. Everything written
/// into the page is HTML-encoded, and the script receives it as a JSON string literal.
/// </para>
/// <para>
/// A query string is carried over only when every character is one a URL query may hold unencoded;
/// otherwise it is dropped and the screen still opens.
/// </para>
/// </summary>
internal static class AppOpenPages
{
    public const string Pattern = AppOpenLink.PathPrefix + "/{**route}";

    private const int MaxQueryLength = 512;

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(Pattern, (string? route, HttpRequest request, IDeepLinkResolver resolver) =>
            {
                var deepLink = TryBuildDeepLink(route, request.QueryString.Value, resolver);

                return deepLink is null
                    ? NotFound()
                    : Page(deepLink);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    /// <summary>
    /// <c>diaspora://{route}{?query}</c>, or null when the route is not one the app can open.
    /// </summary>
    internal static string? TryBuildDeepLink(string? route, string? query, IDeepLinkResolver resolver)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var path = "/" + route.Trim().TrimStart('/');

        if (DeepLink.TryCreate(path) is null || !resolver.IsKnownRoute(path))
        {
            return null;
        }

        // The router hands over the decoded path. Re-encode it a segment at a time so a space or a
        // quote in an identifier reaches the app as data rather than as part of the URL's syntax.
        var encoded = new StringBuilder("diaspora://");
        var segments = path.TrimStart('/').Split('/');

        for (var index = 0; index < segments.Length; index++)
        {
            if (index > 0)
            {
                encoded.Append('/');
            }

            encoded.Append(Uri.EscapeDataString(segments[index]));
        }

        if (IsSafeQuery(query))
        {
            encoded.Append(query);
        }

        return encoded.ToString();
    }

    private static bool IsSafeQuery(string? query)
    {
        if (string.IsNullOrEmpty(query) || query.Length > MaxQueryLength || query[0] != '?')
        {
            return false;
        }

        foreach (var character in query.AsSpan(1))
        {
            var allowed = char.IsAsciiLetterOrDigit(character)
                || "-._~!$&'()*+,;=:@/?%".Contains(character, StringComparison.Ordinal);

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static IResult Page(string deepLink)
    {
        var href = WebUtility.HtmlEncode(deepLink);
        var script = JsonSerializer.Serialize(deepLink);

        return Html(
            "Opening Diaspora",
            "If Diaspora does not open by itself, press the button. If it still does not open, install or update the Diaspora app and try the link again.",
            $"""<a class="button" href="{href}">Open Diaspora</a>""",
            $"<script>location.replace({script});</script>",
            StatusCodes.Status200OK);
    }

    private static IResult NotFound() => Html(
        "This link cannot be opened",
        "It does not point to anything in Diaspora. Open the app and find it from there.",
        action: string.Empty,
        script: string.Empty,
        StatusCodes.Status404NotFound);

    private static IResult Html(string heading, string message, string action, string script, int statusCode)
    {
        var h = WebUtility.HtmlEncode(heading);
        var m = WebUtility.HtmlEncode(message);

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="referrer" content="no-referrer">
            <meta name="robots" content="noindex">
            <title>{{h}}</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 0;
                     display: grid; place-items: center; min-height: 100vh; background: #f7f8fa;
                     color: #14161a; padding: 24px; }
              main { max-width: 22rem; text-align: center; }
              h1 { font-size: 1.25rem; margin: 0 0 .5rem; }
              p { margin: 0 0 1.5rem; line-height: 1.5; color: #55606e; }
              .button { display: inline-block; background: #f59e0b; color: #fff; text-decoration: none;
                        font: inherit; font-weight: 700; padding: .75rem 1.5rem; border-radius: 999px;
                        border: 0; cursor: pointer; }
            </style>
            </head>
            <body>
            <main>
              <h1>{{h}}</h1>
              <p>{{m}}</p>
              {{action}}
            </main>
            {{script}}
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, statusCode);
    }
}
