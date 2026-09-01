using System.Globalization;
using System.Text;

namespace Diaspora.Email;

/// <summary>
/// Wraps a rendered message in the platform's house style, and produces the plain-text twin.
/// </summary>
/// <remarks>
/// <para>
/// <b>The markup lives here, in code, and never in a template.</b> Notification templates are
/// editable reference data: an operator can change one through the admin API without a deploy.
/// Allowing HTML in that content would mean a stored-XSS path through a table, which is why
/// <c>EmailNotificationChannel</c> refuses to produce HTML and says an adapter that wants a
/// house-styled message should wrap it. This is that wrapper.
/// </para>
/// <para>
/// Everything from the template is escaped on the way in. The only HTML in the output is the
/// shell, and the shell is a constant.
/// </para>
/// <para>
/// <b>Both parts, always.</b> A message sent as HTML alone is a message a text-only client, a
/// screen reader in plain mode, and a spam filter all read as empty. Resend takes <c>html</c> and
/// <c>text</c> together and the recipient's client picks; sending only the first is the single
/// most common deliverability mistake.
/// </para>
/// <para>
/// <b>Tables and inline styles, deliberately.</b> Mail clients are not browsers — Outlook renders
/// through Word, Gmail strips <c>&lt;style&gt;</c> blocks, and flexbox is unavailable across most
/// of the estate. This is written the way email has to be written rather than the way a page would
/// be.
/// </para>
/// </remarks>
public static class EmailLayout
{
    /// <summary>Diaspora's primary, matching the client's `primary-500`.</summary>
    private const string Accent = "#F59E0B";

    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Hairline = "#E5E7EB";
    private const string Page = "#F9FAFB";

    /// <summary>
    /// The HTML body for a message.
    /// </summary>
    /// <param name="title">The heading. Escaped.</param>
    /// <param name="body">The message, as plain text or Markdown. Escaped, then paragraphed.</param>
    /// <param name="actionLabel">Optional button text. Escaped.</param>
    /// <param name="actionUrl">Where the button goes. Refused unless absolute http(s).</param>
    /// <param name="footerNote">Optional small print under the rule. Escaped.</param>
    /// <param name="unsubscribeUrl">Adds a one-click unsubscribe line when present.</param>
    public static string Html(
        string title,
        string body,
        string? actionLabel = null,
        string? actionUrl = null,
        string? footerNote = null,
        string? unsubscribeUrl = null)
    {
        var html = new StringBuilder(2048);

        html.Append(CultureInfo.InvariantCulture, $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{Escape(title)}</title></head>
            <body style="margin:0;padding:0;background:{Page};">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Page};padding:24px 12px;">
            <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#FFFFFF;border:1px solid {Hairline};border-radius:12px;">
            <tr><td style="padding:28px 32px 0 32px;">
            <div style="font:700 18px/1.2 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Ink};letter-spacing:-0.2px;">
            Diaspora</div></td></tr>
            <tr><td style="padding:20px 32px 0 32px;">
            <h1 style="margin:0;font:700 22px/1.3 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Ink};">{Escape(title)}</h1>
            </td></tr>
            <tr><td style="padding:12px 32px 0 32px;font:400 15px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Ink};">
            {Paragraphs(body)}
            </td></tr>
            """);

        if (!string.IsNullOrWhiteSpace(actionLabel) && IsSafeUrl(actionUrl))
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td style="padding:24px 32px 0 32px;">
                <a href="{Escape(actionUrl!)}" style="display:inline-block;background:{Accent};color:#111827;text-decoration:none;font:600 15px/1 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;padding:13px 22px;border-radius:9999px;">{Escape(actionLabel!)}</a>
                </td></tr>
                """);
        }

        if (!string.IsNullOrWhiteSpace(footerNote))
        {
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td style="padding:24px 32px 0 32px;">
                <div style="border-top:1px solid {Hairline};padding-top:16px;font:400 13px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Muted};">{Escape(footerNote!)}</div>
                </td></tr>
                """);
        }

        html.Append(CultureInfo.InvariantCulture, $"""
            <tr><td style="padding:24px 32px 28px 32px;">
            <div style="font:400 12px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Muted};">
            Diaspora — your community abroad, all in one.
            </div></td></tr>
            </table>
            """);

        if (IsSafeUrl(unsubscribeUrl))
        {
            // Present in the body as well as the header. Gmail shows its own control from the
            // header, but a member who wants out and cannot find a link reports the message as
            // spam instead, and that costs the sending domain far more than the unsubscribe does.
            html.Append(CultureInfo.InvariantCulture, $"""
                <div style="max-width:560px;padding:14px 8px 0 8px;font:400 12px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:{Muted};text-align:center;">
                <a href="{Escape(unsubscribeUrl!)}" style="color:{Muted};">Unsubscribe from these emails</a>
                </div>
                """);
        }

        html.Append("</td></tr></table></body></html>");

        return html.ToString();
    }

    /// <summary>
    /// The plain-text twin of <see cref="Html"/>, carrying the same facts.
    /// </summary>
    /// <remarks>
    /// Not a stripped copy of the markup. The action becomes a labelled URL on its own line, which
    /// is what a text client can actually act on — a bare href with no label reads as noise.
    /// </remarks>
    public static string Text(
        string title,
        string body,
        string? actionLabel = null,
        string? actionUrl = null,
        string? footerNote = null,
        string? unsubscribeUrl = null)
    {
        var text = new StringBuilder(512);

        text.Append(title).Append("\n\n").Append(body.Trim()).Append('\n');

        if (!string.IsNullOrWhiteSpace(actionLabel) && IsSafeUrl(actionUrl))
        {
            text.Append('\n').Append(actionLabel).Append(":\n").Append(actionUrl).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(footerNote))
        {
            text.Append('\n').Append(footerNote).Append('\n');
        }

        text.Append("\n—\nDiaspora — your community abroad, all in one.\n");

        if (IsSafeUrl(unsubscribeUrl))
        {
            text.Append("Unsubscribe: ").Append(unsubscribeUrl).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Blank-line-separated text becomes paragraphs; single newlines become breaks.
    /// </summary>
    /// <remarks>
    /// The minimum that makes composed copy readable. Deliberately not a Markdown renderer: the
    /// bodies this wraps are one to three sentences, and a renderer here would be a second place
    /// markup could enter the document.
    /// </remarks>
    private static string Paragraphs(string body)
    {
        var paragraphs = Escape(body.Trim())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        var rendered = new StringBuilder(body.Length + 64);

        foreach (var paragraph in paragraphs)
        {
            rendered
                .Append("<p style=\"margin:0 0 12px 0;\">")
                .Append(paragraph.Replace("\n", "<br>", StringComparison.Ordinal))
                .Append("</p>");
        }

        return rendered.ToString();
    }

    /// <summary>
    /// Whether a URL may be placed in an <c>href</c>.
    /// </summary>
    /// <remarks>
    /// Absolute http(s) only. A relative one does not resolve in a mail client, and the scheme
    /// check is what stops a <c>javascript:</c> or <c>data:</c> URL reaching a link — the routes
    /// these come from are reference data, and reference data is editable.
    /// </remarks>
    private static bool IsSafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Neutralises markup without touching anything else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not <c>WebUtility.HtmlEncode</c>, which also rewrites every non-ASCII
    /// character as a numeric entity. That turns <i>Bienvenue à Diaspora</i> into
    /// <c>Bienvenue &amp;#224; Diaspora</c> and an Arabic message into several kilobytes of
    /// escapes — for no gain, because the document declares UTF-8 and those characters are
    /// already legal exactly as they are.
    /// </para>
    /// <para>
    /// What must be encoded is the five characters that can end a text node or an attribute.
    /// All five are encoded everywhere rather than per context: a value that is safe in one
    /// position and not the other is a distinction a caller eventually gets wrong.
    /// </para>
    /// </remarks>
    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
