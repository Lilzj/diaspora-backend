using Microsoft.AspNetCore.Http;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Sets the response headers a JSON API should always send.
/// <para>
/// Modest but not pointless. This host serves JSON to a mobile client, so the headers that
/// matter to a browser matter less — but the API documentation is browsable in Development,
/// error responses can be rendered, and a future admin console will share this origin. The
/// cost is four dictionary writes per response.
/// </para>
/// <para>
/// No Content-Security-Policy: a meaningful one depends on what a page loads, and a
/// blanket <c>default-src 'none'</c> would break the Scalar documentation UI. It belongs
/// with the first HTML surface this host serves, not before it.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;

        // Stops a browser from re-interpreting a JSON error body as HTML and running it.
        headers["X-Content-Type-Options"] = "nosniff";

        // An API has no reason to be framed.
        headers["X-Frame-Options"] = "DENY";

        // Keeps access tokens and member identifiers out of the Referer header on any
        // cross-origin navigation away from an API URL.
        headers["Referrer-Policy"] = "no-referrer";

        // Nothing this host serves needs hardware access.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        return _next(context);
    }
}
