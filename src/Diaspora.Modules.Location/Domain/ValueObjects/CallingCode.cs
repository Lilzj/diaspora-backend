using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An E.164 country calling code, stored with its leading plus, for example <c>+44</c>.
/// <para>
/// Present because the frontend's bundled country list carries one and its phone input
/// renders it. Note that calling codes are not unique — <c>+1</c> covers twenty countries —
/// so this is metadata hanging off a country and never an identifier for one.
/// </para>
/// </summary>
public sealed class CallingCode : ValueObject
{
    public const int MaxDigits = 4;

    public string Value { get; }

    private CallingCode(string value)
    {
        Value = value;
    }

    public static Result<CallingCode> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CallingCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported, "A calling code is required."));
        }

        var digits = value.Trim().TrimStart('+');

        if (digits.Length is 0 or > MaxDigits || !digits.All(char.IsAsciiDigit))
        {
            return Result.Failure<CallingCode>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"'{value}' is not an E.164 country calling code."));
        }

        return Result.Success(new CallingCode($"+{digits}"));
    }

    public static CallingCode Create(string value)
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
