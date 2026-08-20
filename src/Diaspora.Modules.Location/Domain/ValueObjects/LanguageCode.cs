using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An ISO 639-1 two-letter language code, normalised to lower case.
/// <para>
/// Lower case by the standard's own convention, and the opposite of
/// <see cref="CountryCode"/> deliberately: <c>en-GB</c> reads the way it does because the
/// two halves are cased differently, and normalising both to the same case would make every
/// locale string in the platform subtly wrong.
/// </para>
/// </summary>
public sealed class LanguageCode : ValueObject
{
    public const int Length = 2;

    public string Value { get; }

    private LanguageCode(string value)
    {
        Value = value;
    }

    public static Result<LanguageCode> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<LanguageCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported, "A language code is required."));
        }

        var trimmed = value.Trim();

        if (trimmed.Length != Length || !trimmed.All(char.IsAsciiLetter))
        {
            return Result.Failure<LanguageCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"'{trimmed}' is not an ISO 639-1 language code."));
        }

        return Result.Success(new LanguageCode(trimmed.ToLowerInvariant()));
    }

    public static LanguageCode Create(string value)
    {
        var result = TryCreate(value);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
