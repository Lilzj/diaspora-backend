using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// An IANA time zone identifier (for example "Africa/Lagos"). Validated against
/// the host's time zone database so scheduling and "last active" rendering are
/// safe downstream.
/// </summary>
public sealed class TimeZoneIdentifier : ValueObject
{
    public static readonly TimeZoneIdentifier Utc = new("UTC");

    public string Value { get; }

    private TimeZoneIdentifier(string value)
    {
        Value = value;
    }

    public static TimeZoneIdentifier Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = value.Trim();
        if (!IsKnown(normalized))
            throw new ArgumentException($"'{value}' is not a known IANA time zone identifier.", nameof(value));
        return new TimeZoneIdentifier(normalized);
    }

    public static bool TryCreate(string? value, out TimeZoneIdentifier? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim();
        if (!IsKnown(normalized)) return false;

        result = new TimeZoneIdentifier(normalized);
        return true;
    }

    private static bool IsKnown(string value)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static implicit operator string(TimeZoneIdentifier identifier) => identifier.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
