using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Sets the response headers every response from this host should carry.
/// <para>
/// Modest but not pointless. This host serves JSON to a mobile client, so the headers that
/// matter to a browser matter less — but the API documentation is browsable in Development,
/// error responses can be rendered, a future admin console will share this origin, and the host
/// serves a few pages of its own. The cost is five dictionary writes per response.
/// </para>
/// <para>
/// <b>Content-Security-Policy: everything forbidden, unless the endpoint says otherwise</b>
/// (BSEC-010). This middleware used to send none, on the grounds that a policy belonged with the
/// first HTML surface this host served. Four arrived and none brought one, because nothing noticed
/// that moment pass. So the default is now the strictest policy there is, on every response, and
/// an endpoint that serves a page replaces it with one naming what that page needs: the host's own
/// pages through <see cref="Hosting.HostPage"/>, an invoice document with its own. A page that
/// forgets is not served without a policy. It is served with this one, and loses its own style and
/// script in front of whoever wrote it. On JSON the policy changes nothing; on anything a browser
/// renders from this origin by mistake, an error body or an uploaded file, it is a backstop.
/// </para>
/// <para>
/// The one exemption is Scalar's API reference, which runs scripts of its own and is mapped only
/// in Development, at <see cref="ApiReferencePath"/>. In every other environment that path is sent
/// the default like everything else.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>
    /// Nothing may load, run, connect, submit, change the base URL or frame the response. Sent on
    /// every response whose endpoint does not send a policy of its own.
    /// </summary>
    public const string DefaultPolicy =
        "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    /// <summary>Where Program maps Scalar's API reference, in Development only.</summary>
    public const string ApiReferencePath = "/scalar";

    private readonly RequestDelegate _next;
    private readonly bool _servesApiReference;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _next = next;
        _servesApiReference = environment.IsDevelopment();
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

        // Set before the endpoint runs, so a page's own policy replaces this one rather than
        // joining it. A browser enforces every policy a response carries, so a page sent this one
        // as well as its own would still have its style and script refused.
        if (!(_servesApiReference && context.Request.Path.StartsWithSegments(ApiReferencePath)))
        {
            headers.ContentSecurityPolicy = DefaultPolicy;
        }

        return _next(context);
    }
}
