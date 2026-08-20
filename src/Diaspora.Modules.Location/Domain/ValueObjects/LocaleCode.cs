using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A BCP 47 language tag of the form <c>language-REGION</c>, for example <c>en-GB</c>.
/// <para>
/// Composed from a <see cref="LanguageCode"/> and a <see cref="CountryCode"/> rather than
/// parsed from a free string, which makes the two halves independently correct by
/// construction. Identity owns the member's *preferred* locale; Location owns the *default*
/// locale of a place, which is what a client falls back to before the member has expressed
/// a preference.
/// </para>
/// </summary>
public sealed class LocaleCode : ValueObject
{
    public LanguageCode Language { get; }

    public CountryCode Region { get; }

    private LocaleCode(LanguageCode language, CountryCode region)
    {
        Language = language;
        Region = region;
    }

    public static LocaleCode Create(LanguageCode language, CountryCode region)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(region);

        return new LocaleCode(language, region);
    }

    public static Result<LocaleCode> TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<LocaleCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported, "A locale is required."));
        }

        var parts = value.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return Result.Failure<LocaleCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"'{value}' is not a language-REGION tag."));
        }

        var language = LanguageCode.TryCreate(parts[0]);

        if (language.IsFailure)
        {
            return Result.Failure<LocaleCode>(language.Error);
        }

        var region = CountryCode.TryCreate(parts[1]);

        return region.IsFailure
            ? Result.Failure<LocaleCode>(region.Error)
            : Result.Success(new LocaleCode(language.Value, region.Value));
    }

    public string Value => $"{Language.Value}-{Region.Value}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Language;
        yield return Region;
    }

    public override string ToString() => Value;
}
