using System.Globalization;

namespace Diaspora.Modules.Notifications.Domain.Policies;

/// <summary>
/// Which languages to try, in order, when rendering for a member.
/// <para>
/// <b>The chain is the difference between a localized platform and a broken one.</b> A member whose
/// language is <c>pt-BR</c> and whose template has only <c>pt</c> must get Portuguese, not English —
/// and a member whose language is <c>yo</c> with no Yoruba template must get English rather than an
/// empty message. Both are the same walk: exact tag, then the neutral parent, then the platform
/// default, then whatever the template calls its own default.
/// </para>
/// <para>
/// <b>No machine translation, ever.</b> A wrong translation of "your account has been suspended" is
/// worse than an English one, and this platform's members read English as a second language far more
/// often than they read a machine's guess at their first.
/// </para>
/// </summary>
public static class LocaleFallbackChain
{
    /// <summary>The tag used when a member has stated no preference and no template matches.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>
    /// The tags to try, most specific first, de-duplicated and never empty.
    /// </summary>
    public static IReadOnlyList<string> For(string? preferred)
    {
        var chain = new List<string>(4);

        void Add(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            var normalized = Normalize(tag);

            if (!chain.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(normalized);
            }
        }

        Add(preferred);
        Add(NeutralParentOf(preferred));
        Add(DefaultLanguage);

        return chain;
    }

    /// <summary>
    /// <c>pt-BR</c> becomes <c>pt</c>. Null when the tag has no region to drop.
    /// </summary>
    public static string? NeutralParentOf(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var separator = tag.IndexOf('-', StringComparison.Ordinal);

        return separator > 0 ? tag[..separator] : null;
    }

    /// <summary>
    /// Lower-cases the language and upper-cases the region, which is the BCP-47 convention every
    /// client sends and half of them send wrong.
    /// </summary>
    public static string Normalize(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var trimmed = tag.Trim();
        var separator = trimmed.IndexOf('-', StringComparison.Ordinal);

        if (separator <= 0)
        {
            return trimmed.ToLowerInvariant();
        }

        return string.Concat(
            trimmed[..separator].ToLowerInvariant(),
            "-",
            trimmed[(separator + 1)..].ToUpperInvariant());
    }

    /// <summary>
    /// Whether the tag names a culture the runtime knows, so date and currency formatting will
    /// behave. An unknown tag is not rejected — it simply formats with the invariant culture.
    /// </summary>
    public static bool IsKnownCulture(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(tag);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
