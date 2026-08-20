using System.Globalization;
using System.Text;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A URL-safe, lower-case, hyphenated name for a place — <c>peckham</c>, <c>rose-hill</c>.
/// <para>
/// This exists because the frontend already routes on one. <c>NeighborhoodCard</c> builds
/// its link with <c>name.toLowerCase().replace(/\s+/g, '-')</c> and the hub screen resolves
/// the result, so a backend that only accepted GUIDs would break a navigation path that
/// works today. Slugs are therefore first-class: stored, indexed and resolvable alongside
/// the identifier.
/// </para>
/// <para>
/// Generation strips diacritics rather than dropping the characters, so <c>Curepipe</c> and
/// <c>Curépipe</c> produce the same slug instead of two different ones.
/// </para>
/// </summary>
public sealed class LocationSlug : ValueObject
{
    public const int MaxLength = 120;

    public string Value { get; }

    private LocationSlug(string value)
    {
        Value = value;
    }

    public static Result<LocationSlug> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<LocationSlug>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "A slug is required."));
        }

        var slug = Slugify(value);

        if (slug.Length == 0)
        {
            return Result.Failure<LocationSlug>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                $"'{value}' contains no characters that can form a slug."));
        }

        return Result.Success(new LocationSlug(
            slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-')));
    }

    public static LocationSlug Create(string value)
    {
        var result = TryCreate(value);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    /// <summary>
    /// Builds a slug that disambiguates a place from its namesakes by qualifying it with its
    /// parent — <c>springfield-illinois</c>. There are twelve Springfields; a slug that does
    /// not say which one is a 404 waiting for the second import.
    /// </summary>
    public static LocationSlug CreateQualified(string name, string? parentName)
    {
        var qualified = string.IsNullOrWhiteSpace(parentName) ? name : $"{name}-{parentName}";
        return Create(qualified);
    }

    private static string Slugify(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasHyphen = false;

        foreach (var character in decomposed)
        {
            // Combining marks are the accents FormD just split off. Dropping them is what
            // folds "Curépipe" onto "curepipe" rather than onto "curpipe".
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasHyphen = false;
                continue;
            }

            if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>Whether a raw string is already a well-formed slug, without allocating one.</summary>
    public static bool IsWellFormed(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') &&
        !value.StartsWith('-') &&
        !value.EndsWith('-');

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
