using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// The stable, human-readable handle a provider is linked by.
/// <para>
/// Exists so a deep link survives a rename. <c>/provider/mama-afrika-kitchen</c> is what goes
/// into a WhatsApp message, a printed flyer and a Google result, and a provider that renames
/// itself must not break every one of them — so the slug is derived from the name once, at
/// registration, and never recomputed. Changing it is an explicit, permissioned act.
/// </para>
/// <para>
/// Uniqueness is enforced by the database, not here: a value object cannot know what other
/// values exist, and a uniqueness check in application code is a race by construction. The
/// aggregate asks its repository for a free slug and takes a numeric suffix when the base is
/// taken, which is why <see cref="WithSuffix"/> exists.
/// </para>
/// </summary>
public sealed class ProviderSlug : ValueObject
{
    public const int MinLength = 2;

    public const int MaxLength = 120;

    private ProviderSlug(string value) => Value = value;

    private ProviderSlug() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Validates a slug a caller supplied. Strict — no silent repair.</summary>
    public static Result<ProviderSlug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<ProviderSlug>(ProviderErrors.SlugInvalid);
        }

        var candidate = value.Trim().ToLowerInvariant();

        if (candidate.Length is < MinLength or > MaxLength)
        {
            return Result.Failure<ProviderSlug>(ProviderErrors.SlugInvalid);
        }

        var valid = candidate.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !candidate.StartsWith('-')
            && !candidate.EndsWith('-')
            && !candidate.Contains("--", StringComparison.Ordinal);

        return valid
            ? Result.Success(new ProviderSlug(candidate))
            : Result.Failure<ProviderSlug>(ProviderErrors.SlugInvalid);
    }

    /// <summary>
    /// The slug a name with no ASCII content falls back to. Never unique on its own — the
    /// registration path resolves the collision with <see cref="WithSuffix"/>, which is the
    /// same mechanism that handles two restaurants genuinely called the same thing.
    /// </summary>
    public const string Fallback = "provider";

    /// <summary>
    /// Derives a slug from a name. Lenient — this path never fails, because a provider whose
    /// name is entirely non-ASCII (which is ordinary here: "Église Évangélique", "مطعم") must
    /// still be registrable, and refusing them would be the module telling a diaspora platform
    /// that diaspora names are invalid.
    /// </summary>
    public static ProviderSlug FromName(ProviderName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var slugified = Slugify(name.Value);

        return new ProviderSlug(slugified.Length >= MinLength ? slugified : Fallback);
    }

    /// <summary>The same slug with a numeric suffix, for resolving a collision.</summary>
    public ProviderSlug WithSuffix(int suffix)
    {
        var tail = $"-{suffix}";
        var head = Value.Length + tail.Length <= MaxLength
            ? Value
            : Value[..(MaxLength - tail.Length)].TrimEnd('-');

        return new ProviderSlug(head + tail);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Apostrophes and their typographic cousins, which are dropped rather than replaced.
    /// <para>
    /// An apostrophe sits <em>inside</em> a word, so treating it as a separator produces
    /// <c>mama-s-kitchen</c> — which reads badly, is hard to say over the phone, and is not what
    /// anybody would have typed. Dropping it gives <c>mamas-kitchen</c>. The curly variants are
    /// here because a phone keyboard produces them by default, so most real names arrive with
    /// one.
    /// </para>
    /// </summary>
    private static readonly char[] Elided = ['\'', '’', 'ʼ', '`'];

    private static string Slugify(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (Array.IndexOf(Elided, character) >= 0)
            {
                continue;
            }

            if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
    }
}
