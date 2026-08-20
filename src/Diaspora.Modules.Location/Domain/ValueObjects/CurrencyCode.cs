using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An ISO 4217 currency code, normalised to upper case.
/// <para>
/// Country metadata carries this so that Marketplace, Housing, Jobs and Community Life stop
/// each guessing a default currency from a city name. The frontend already types
/// <c>currency</c> as a free string on four DTOs with a comment listing examples — that
/// comment is the specification this type replaces.
/// </para>
/// </summary>
public sealed class CurrencyCode : ValueObject
{
    public const int Length = 3;

    public string Value { get; }

    private CurrencyCode(string value)
    {
        Value = value;
    }

    public static Result<CurrencyCode> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CurrencyCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported, "A currency code is required."));
        }

        var trimmed = value.Trim();

        if (trimmed.Length != Length || !trimmed.All(char.IsAsciiLetter))
        {
            return Result.Failure<CurrencyCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"'{trimmed}' is not an ISO 4217 currency code."));
        }

        return Result.Success(new CurrencyCode(trimmed.ToUpperInvariant()));
    }

    public static CurrencyCode Create(string value)
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
