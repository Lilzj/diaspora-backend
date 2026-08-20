using System.Globalization;
using System.Text;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// The stable, shareable part of a listing's or a landlord's web address.
/// <para>
/// <b>It is also the tie-break every cursor page orders on</b>, which is why it is unique,
/// indexed and a string: the primary key is a value-converted identifier EF cannot compare in
/// SQL, so the slug is what makes "the row after this one" expressible. B7, B8, B9 and B10 each
/// record the same constraint.
/// </para>
/// <para>
/// Diacritics are folded rather than dropped, so a listing in Reunion or Cote d'Ivoire keeps a
/// readable address instead of losing half its letters.
/// </para>
/// </summary>
public sealed class HousingSlug : ValueObject
{
    private HousingSlug(string value) => Value = value;

    private HousingSlug() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Builds a slug from arbitrary text, appending a discriminator when one is given.</summary>
    public static HousingSlug Create(string? source, string? discriminator = null)
    {
        var slug = Slugify(source) ?? "property";

        if (!string.IsNullOrWhiteSpace(discriminator))
        {
            var suffix = Slugify(discriminator);

            if (suffix is not null)
            {
                slug = $"{slug}-{suffix}";
            }
        }

        return new HousingSlug(Clamp(slug));
    }

    /// <summary>Parses a slug supplied by a caller, refusing anything that is not one.</summary>
    public static Result<HousingSlug> TryCreate(string? value)
    {
        var slug = Slugify(value);

        return slug is null
            ? Result.Failure<HousingSlug>(HousingErrors.SlugInvalid)
            : Result.Success(new HousingSlug(Clamp(slug)));
    }

    public static string? Slugify(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var folded = source.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(folded.Length);
        var lastWasHyphen = false;

        foreach (var character in folded)
        {
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

        var slug = builder.ToString().Trim('-');

        return slug.Length == 0 ? null : slug;
    }

    private static string Clamp(string slug) =>
        slug.Length > HousingLimits.MaxSlug
            ? slug[..HousingLimits.MaxSlug].TrimEnd('-')
            : slug;

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
