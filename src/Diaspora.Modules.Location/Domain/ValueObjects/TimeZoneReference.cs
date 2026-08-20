using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An IANA time zone identifier, for example <c>Europe/London</c> or <c>Indian/Mauritius</c>.
/// <para>
/// IANA rather than a UTC offset, and the distinction is not pedantic: an offset is wrong
/// for half the year in every country that observes daylight saving. The frontend's event
/// DTO already carries a <c>timezone</c> string and its create form already collects one —
/// this is the type that makes those strings mean something.
/// </para>
/// <para>
/// Resolution is left to <see cref="TimeZoneInfo"/> at the point of use rather than done
/// here, because the tz database updates independently of this code and a value that was
/// valid when stored must not become unconstructable when a zone is renamed.
/// </para>
/// </summary>
public sealed class TimeZoneReference : ValueObject
{
    public const int MaxLength = 64;

    public string Value { get; }

    private TimeZoneReference(string value)
    {
        Value = value;
    }

    public static Result<TimeZoneReference> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<TimeZoneReference>(Error.Validation(
                LocationErrorCodes.CountryNotSupported, "A time zone identifier is required."));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<TimeZoneReference>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"A time zone identifier may not exceed {MaxLength} characters."));
        }

        // Shape only: letters, digits, and the separators IANA actually uses. Anything else
        // is a caller mistake; a well-formed identifier the local tz database does not know
        // is not.
        var wellFormed = trimmed.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '/' or '_' or '-' or '+');

        return wellFormed
            ? Result.Success(new TimeZoneReference(trimmed))
            : Result.Failure<TimeZoneReference>(Error.Validation(
                LocationErrorCodes.CountryNotSupported,
                $"'{trimmed}' is not a well-formed IANA time zone identifier."));
    }

    public static TimeZoneReference Create(string value)
    {
        var result = TryCreate(value);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    /// <summary>
    /// The zone if the host's tz database knows it, otherwise <c>null</c>.
    /// <para>
    /// Null rather than an exception because a missing zone is an operational fact about the
    /// host, not a defect in the stored value, and a country page should still render when
    /// one row's zone is unrecognised.
    /// </para>
    /// </summary>
    public TimeZoneInfo? Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(Value);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
