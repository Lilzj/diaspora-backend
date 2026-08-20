using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An ISO 3166-1 alpha-2 country code, normalised to upper case.
/// <para>
/// Every country reference on the platform passes through this type. The reason it exists
/// rather than a bare <c>string</c> is casing: the frontend sends <c>GB</c>, the IP lookup
/// returns <c>gb</c>, and a member's declared country arrives however they typed it. Three
/// spellings of one country produce three rows, three reputation ladders and three sets of
/// verified residency — a class of bug that is invisible until it is expensive.
/// </para>
/// <para>
/// Validated on shape, not against a fixed list. ISO reassigns codes, and a hard-coded set
/// in this file would be wrong within a year and wrong in a way nobody notices. Whether a
/// well-formed code names a country the platform actually serves is
/// <see cref="Countries.Country"/>'s question.
/// </para>
/// </summary>
public sealed class CountryCode : ValueObject
{
    public const int Length = 2;

    public string Value { get; }

    private CountryCode(string value)
    {
        Value = value;
    }

    public static Result<CountryCode> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CountryCode>(Error.Validation(
                LocationErrorCodes.CountryNotFound, "A country code is required."));
        }

        var trimmed = value.Trim();

        if (trimmed.Length != Length ||
            !char.IsAsciiLetter(trimmed[0]) ||
            !char.IsAsciiLetter(trimmed[1]))
        {
            return Result.Failure<CountryCode>(Error.Validation(
                LocationErrorCodes.CountryNotFound,
                $"'{trimmed}' is not an ISO 3166-1 alpha-2 country code."));
        }

        return Result.Success(new CountryCode(trimmed.ToUpperInvariant()));
    }

    public static CountryCode Create(string value)
    {
        var result = TryCreate(value);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    /// <summary>
    /// The flag emoji for this country.
    /// <para>
    /// Derived, not stored. A flag is the two letters of the code shifted into the
    /// regional-indicator block, so a lookup table of 250 emoji would be three copies of the
    /// same fact and two of them stale. The Trust module derives it the same way; both
    /// derive from the standard rather than from each other, so neither owns the other.
    /// </para>
    /// </summary>
    public string ToFlagEmoji()
    {
        const int regionalIndicatorBase = 0x1F1E6;

        return string.Concat(
            char.ConvertFromUtf32(regionalIndicatorBase + (Value[0] - 'A')),
            char.ConvertFromUtf32(regionalIndicatorBase + (Value[1] - 'A')));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
