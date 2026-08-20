using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Knowledge;

/// <summary>
/// How the Knowledge Directory normalizes the four kinds of text that are identity rather than
/// prose: a key, a country code, a locale tag and a bounded label.
/// <para>
/// <b>One implementation, because a second one is a defect that cannot be seen.</b> Five
/// aggregates in this phase accept a category key, and a key normalized to lower case in four of
/// them and left alone in the fifth produces a directory entry that points at a category the
/// database says does not exist — with both rows visible, both spellings plausible, and nothing in
/// a stack trace. B8 already learned the same thing about its own category keys; this is that
/// lesson applied before it is paid for again.
/// </para>
/// <para>
/// Every method returns null for input it will not accept, rather than throwing or silently
/// repairing. The caller turns that into the right typed <c>Error</c>, which is a decision only
/// the caller can make: a bad key on a create is a validation failure, and a bad key on a lookup
/// is simply a miss.
/// </para>
/// </summary>
public static class KnowledgeNormalization
{
    /// <summary>
    /// A stable key: lower-case ASCII letters, digits and single interior hyphens.
    /// <para>
    /// The same grammar B8 uses for guide category keys, deliberately — the directory draws from
    /// those keys, and two grammars would mean a key that is legal on one side of the join and
    /// not the other.
    /// </para>
    /// <para>
    /// <b>Exactly one parameter, and that is not a stylistic choice.</b> This method is passed as a
    /// method group to <c>Select</c> in half a dozen places, and an optional second <c>int</c>
    /// parameter makes it match <c>Select(Func&lt;T, int, TResult&gt;)</c> — LINQ's <em>indexed</em>
    /// overload. The compiler picks it silently, every key is then validated against its own
    /// position in the list as a length limit, and the result is a directory that arranges seven of
    /// twenty-four rows with no error anywhere. This cost an afternoon to find; a separate
    /// <see cref="PathKey"/> removes the trap rather than asking every future call site to remember
    /// a lambda.
    /// </para>
    /// </summary>
    public static string? Key(string? value) => Key(value, KnowledgeLimits.MaxCategoryKey);

    /// <summary>
    /// A learning path key, which is allowed to be longer than a category key because it is a
    /// phrase rather than a taxonomy term — <c>arriving-with-school-age-children</c>.
    /// </summary>
    public static string? PathKey(string? value) => Key(value, KnowledgeLimits.MaxPathKey);

    private static string? Key(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim().ToLowerInvariant();

        var valid = candidate.Length <= maxLength
            && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid ? candidate : null;
    }

    /// <summary>
    /// An upper-case ISO 3166-1 alpha-2 country code.
    /// <para>
    /// Shape only — this does not check the code against B4's catalogue, and must not. The
    /// directory is a discovery layer over knowledge, not a second registry of countries, and a
    /// deployment that serves a territory B4 has not catalogued yet should be able to publish
    /// guidance about it rather than being told its country does not exist.
    /// </para>
    /// </summary>
    public static string? Country(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim().ToUpperInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterUpper)
            ? candidate
            : null;
    }

    /// <summary>
    /// A BCP-47 locale tag, lower-cased on the language and upper-cased on the region:
    /// <c>fr</c>, <c>fr-MU</c>, <c>pt-BR</c>.
    /// <para>
    /// Normalizing the case is what makes the localization collection's key work. Left alone,
    /// <c>fr-mu</c> and <c>fr-MU</c> are two rows, the second insert fails on the unique key, and
    /// the curator is told their translation already exists when the visible list says otherwise.
    /// </para>
    /// </summary>
    public static string? Locale(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > KnowledgeLimits.MaxLocale)
        {
            return null;
        }

        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is 0 or > 3)
        {
            return null;
        }

        if (parts[0].Length is < 2 or > 8 || !parts[0].All(char.IsAsciiLetter))
        {
            return null;
        }

        var segments = new List<string> { parts[0].ToLowerInvariant() };

        foreach (var part in parts.Skip(1))
        {
            if (!part.All(char.IsAsciiLetterOrDigit))
            {
                return null;
            }

            // A two-letter subtag is a region and is conventionally upper-cased; a four-letter one
            // is a script and is title-cased. Anything else is a variant, left as written.
            segments.Add(part.Length switch
            {
                2 => part.ToUpperInvariant(),
                4 => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant(),
                _ => part.ToLowerInvariant()
            });
        }

        return string.Join('-', segments);
    }

    /// <summary>
    /// Trims a label and clips it to its column width, returning null for blank input.
    /// <para>
    /// Clipping rather than rejecting, because these are descriptions and notes — prose whose
    /// exact length is not a business rule. Where the length <em>is</em> a rule, the aggregate
    /// checks it and returns a failure instead of calling this.
    /// </para>
    /// </summary>
    public static string? Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
