using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Diaspora.Api.Hosting;

/// <summary>
/// The host's own HTML pages, and the Content-Security-Policy each is sent with (BSEC-010).
/// <para>
/// Four surfaces use it: the open-route hand-off (<see cref="AppOpenPages"/>), the two Stripe
/// Checkout return pages, and the email confirmation pages. They were three copies of one template,
/// each with its own stylesheet and none with a policy. Now there is one template, and a page's
/// policy is derived from what the page holds: its stylesheet by hash, the hand-off script by hash
/// when the page carries it, a form post back to this host when the page has a form, and nothing
/// else. A response that sends no policy of its own still gets the one that forbids everything
/// (<see cref="Middleware.SecurityHeadersMiddleware.DefaultPolicy"/>).
/// </para>
/// <para>
/// <b>Hashes, not <c>'unsafe-inline'</c>.</b> An inline allowance admits every inline script, an
/// injected one included, and an injected script is the one thing this policy is a backstop
/// against. A hash admits one exact text. So nothing a page styles or runs itself with may vary by
/// request: the stylesheet is a constant, and the hand-off script reads its destination from the
/// page's own link instead of carrying it, which is what lets one hash serve every deep link.
/// </para>
/// <para>
/// <b>Each block is one line.</b> A browser hashes an inline block as it parsed it, after turning
/// every CR LF into LF. A raw string literal takes its line endings from the source file, and a
/// Windows checkout's are CR LF, so a block spread over lines would be hashed one way here and
/// another way by the browser, and the page would lose its style with no error anywhere but the
/// browser's console.
/// </para>
/// </summary>
internal sealed class HostPage : IResult
{
    /// <summary>The one stylesheet every page carries.</summary>
    internal const string Stylesheet =
        "body{font-family:system-ui,-apple-system,\"Segoe UI\",sans-serif;margin:0;display:grid;"
        + "place-items:center;min-height:100vh;background:#f7f8fa;color:#14161a;padding:24px}"
        + "main{max-width:22rem;text-align:center}"
        + "h1{font-size:1.25rem;margin:0 0 .5rem}"
        + "p{margin:0 0 1.5rem;line-height:1.5;color:#55606e}"
        + ".button{display:inline-block;background:#f59e0b;color:#fff;text-decoration:none;font:inherit;"
        + "font-weight:700;padding:.75rem 1.5rem;border-radius:999px;border:0;cursor:pointer}";

    /// <summary>
    /// Follows the page's own link as soon as the page loads. It reads the destination from the link
    /// rather than carrying it, so its text, and with it its hash, is the same on every page.
    /// </summary>
    internal const string HandOffScript =
        "var link=document.querySelector(\"a.button\");if(link){location.replace(link.getAttribute(\"href\"));}";

    private static readonly string StyleSource = HashSource(Stylesheet);

    private static readonly string ScriptSource = HashSource(HandOffScript);

    private readonly string _html;
    private readonly string _policy;
    private readonly int _statusCode;

    private HostPage(string html, string policy, int statusCode)
    {
        _html = html;
        _policy = policy;
        _statusCode = statusCode;
    }

    /// <summary>A page that says something and offers nothing.</summary>
    public static HostPage Message(string heading, string message, int statusCode = StatusCodes.Status200OK) =>
        Render(heading, message, action: string.Empty, handOff: false, postsForm: false, statusCode);

    /// <summary>
    /// A page with a link into the app. With <paramref name="handOff"/> it also follows the link as
    /// soon as it loads, and the link is there for when that automatic hand-off does not fire.
    /// </summary>
    public static HostPage Link(string heading, string message, string href, string label, bool handOff) =>
        Render(
            heading,
            message,
            $"""<a class="button" href="{WebUtility.HtmlEncode(href)}">{WebUtility.HtmlEncode(label)}</a>""",
            handOff,
            postsForm: false,
            StatusCodes.Status200OK);

    /// <summary>A page whose button posts <paramref name="token"/> back to this host.</summary>
    public static HostPage Form(string heading, string message, string action, string token, string label) =>
        Render(
            heading,
            message,
            $"""
            <form method="post" action="{WebUtility.HtmlEncode(action)}">
                <input type="hidden" name="token" value="{WebUtility.HtmlEncode(token)}">
                <button class="button" type="submit">{WebUtility.HtmlEncode(label)}</button>
              </form>
            """,
            handOff: false,
            postsForm: true,
            StatusCodes.Status200OK);

    /// <summary>Sends the page with its policy, so that nothing can send the one without the other.</summary>
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers.ContentSecurityPolicy = _policy;

        return Results.Content(_html, "text/html; charset=utf-8", Encoding.UTF8, _statusCode)
            .ExecuteAsync(httpContext);
    }

    /// <summary>
    /// The source expression that admits exactly <paramref name="block"/> as an inline style or
    /// script: <c>'sha256-</c>, the base64 of the SHA-256 of its UTF-8 bytes, and <c>'</c>.
    /// </summary>
    internal static string HashSource(string block) =>
        "'sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(block))) + "'";

    /// <summary>
    /// Nothing may load, connect, frame the page or change its base. The stylesheet may apply, the
    /// hand-off script may run when the page carries it, and the form may post back to this host
    /// when the page has one.
    /// </summary>
    internal static string Policy(bool handOff, bool postsForm) =>
        $"default-src 'none'; style-src {StyleSource}; "
        + (handOff ? $"script-src {ScriptSource}; " : string.Empty)
        + $"base-uri 'none'; form-action {(postsForm ? "'self'" : "'none'")}; frame-ancestors 'none'";

    private static HostPage Render(
        string heading, string message, string action, bool handOff, bool postsForm, int statusCode)
    {
        var h = WebUtility.HtmlEncode(heading);
        var m = WebUtility.HtmlEncode(message);
        var script = handOff ? $"<script>{HandOffScript}</script>" : string.Empty;

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="referrer" content="no-referrer">
            <meta name="robots" content="noindex">
            <title>{{h}}</title>
            <style>{{Stylesheet}}</style>
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

        return new HostPage(html, Policy(handOff, postsForm), statusCode);
    }
}
