using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// The three kinds of text Country Intelligence normalizes that no earlier phase needed: a URL, a
/// telephone number and an official reference number.
/// <para>
/// <b>Keys, country codes, locales and bounded prose are deliberately absent — they delegate to
/// <see cref="KnowledgeNormalization"/>.</b> Notice categories, guide categories and navigation rows
/// all share one key grammar, and a key normalized to lower case in two of them and left alone in
/// the third produces a category the database says does not exist, with both spellings plausible and
/// nothing in a stack trace. B8.1 records that exact defect; a second normalizer here would be it
/// happening again with a different name.
/// </para>
/// <para>
/// Every method returns null for input it will not accept, rather than throwing or silently
/// repairing. The caller turns that into the right typed error, which is a decision only the caller
/// can make: a bad URL on a create is a validation failure, and a bad one on an import is a row to
/// skip.
/// </para>
/// </summary>
public static class IntelligenceNormalization
{
    /// <inheritdoc cref="KnowledgeNormalization.Key(string?)" />
    public static string? Key(string? value) => KnowledgeNormalization.Key(value);

    /// <inheritdoc cref="KnowledgeNormalization.Country(string?)" />
    public static string? Country(string? value) => KnowledgeNormalization.Country(value);

    /// <inheritdoc cref="KnowledgeNormalization.Locale(string?)" />
    public static string? Locale(string? value) => KnowledgeNormalization.Locale(value);

    /// <inheritdoc cref="KnowledgeNormalization.Text(string?, int)" />
    public static string? Text(string? value, int maxLength) =>
        KnowledgeNormalization.Text(value, maxLength);

    /// <summary>
    /// An absolute http or https URL, or null.
    /// <para>
    /// <b>Absolute, and only those two schemes.</b> A relative link on an official source field
    /// would resolve against whatever page happened to render it; <c>javascript:</c> and
    /// <c>data:</c> are the two that turn an editorial field into an injection surface, and refusing
    /// everything except the two schemes a government portal actually uses is a shorter list to get
    /// right than the list of things to block.
    /// </para>
    /// <para>
    /// The host is lower-cased and the rest is left exactly as written: query strings on government
    /// portals are frequently case-sensitive, and helpfully normalizing one is how a link that
    /// worked in the editor 404s in production.
    /// </para>
    /// </summary>
    public static string? Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > IntelligenceLimits.MaxUrl)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var scheme = uri.Scheme;

        if (!string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            && !string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return null;
        }

        return string.IsNullOrEmpty(uri.Host) ? null : uri.AbsoluteUri;
    }

    /// <summary>
    /// The registrable host of a URL, lower-cased — what a source policy actually inspects.
    /// <para>
    /// Separate from <see cref="Url"/> because a validator asking "is this a government domain" wants
    /// the host and nothing else, and every validator parsing the URL for itself is how two of them
    /// end up disagreeing about what <c>HTTPS://GOV.MU:443/x</c> is.
    /// </para>
    /// </summary>
    public static string? Host(string? url)
    {
        var normalized = Url(url);

        return normalized is not null && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : null;
    }

    /// <summary>
    /// A telephone number, kept close to how it was written.
    /// <para>
    /// <b>Deliberately not reformatted to E.164.</b> An emergency number is frequently not a
    /// dialable international number at all — <c>112</c>, <c>999</c>, <c>15</c> — and a normalizer
    /// that insisted on a country code would either mangle them or refuse them. What this does is
    /// strip presentation whitespace, keep the characters a dialer understands, and refuse anything
    /// with no digit in it.
    /// </para>
    /// </summary>
    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var kept = new string([.. value
            .Where(character =>
                char.IsAsciiDigit(character)
                || character is '+' or '-' or '(' or ')' or '.' or '#' or '*' or ' ' or ',')]);

        var collapsed = string.Join(' ', kept.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length is > 0 and <= IntelligenceLimits.MaxPhone
            && collapsed.Any(char.IsAsciiDigit)
            ? collapsed
            : null;
    }

    /// <summary>
    /// An email address, lower-cased.
    /// <para>
    /// Shape only, and shape is all a directory entry needs: an embassy address is transcribed from
    /// a published page rather than proven by a confirmation link, so the check that matters is that
    /// somebody has not pasted a sentence into the field.
    /// </para>
    /// </summary>
    public static string? Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.Length > IntelligenceLimits.MaxEmail || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == trimmed.LastIndexOf('@')
            && at < trimmed.Length - 1
            && trimmed.IndexOf('.', at) > at + 1
            && !trimmed.EndsWith('.')
                ? trimmed
                : null;
    }

    /// <summary>
    /// An official reference number — a circular, gazette or file number.
    /// <para>
    /// Case is preserved. Reference numbers are quoted back to the issuing office by members who
    /// need to be told what the office recognises, and <c>MHA/IMM/2026/014</c> lower-cased is a
    /// string a clerk will not find.
    /// </para>
    /// </summary>
    public static string? Reference(string? value) =>
        Text(value, IntelligenceLimits.MaxReferenceNumber);
}
