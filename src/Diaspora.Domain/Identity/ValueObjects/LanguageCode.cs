using System.Text.RegularExpressions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// A BCP 47 language tag (for example "en", "fr", "pt-BR"). Drives localisation
/// of validation messages, OTP delivery and notifications.
/// </summary>
public sealed class LanguageCode : ValueObject
{
    private static readonly Regex Bcp47Pattern = new(
        "^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$",
        RegexOptions.Compiled);

    public static readonly LanguageCode Default = new("en");

    public string Value { get; }

    private LanguageCode(string value)
    {
        Value = value;
    }

    public static LanguageCode Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = Normalize(value);
        if (!Bcp47Pattern.IsMatch(normalized))
            throw new ArgumentException($"'{value}' is not a valid BCP 47 language tag.", nameof(value));
        return new LanguageCode(normalized);
    }

    public static bool TryCreate(string? value, out LanguageCode? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = Normalize(value);
        if (!Bcp47Pattern.IsMatch(normalized)) return false;

        result = new LanguageCode(normalized);
        return true;
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().Replace('_', '-');
        var segments = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return string.Empty;

        segments[0] = segments[0].ToLowerInvariant();
        for (var i = 1; i < segments.Length; i++)
        {
            segments[i] = segments[i].ToUpperInvariant();
        }

        return string.Join('-', segments);
    }

    /// <summary>
    /// The primary subtag ("pt" for "pt-BR"), used when a translation exists only
    /// for the base language.
    /// </summary>
    public string PrimarySubtag => Value.Split('-')[0];

    public static implicit operator string(LanguageCode code) => code.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
