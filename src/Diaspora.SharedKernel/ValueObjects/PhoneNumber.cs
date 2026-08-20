using System.Text.RegularExpressions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class PhoneNumber : ValueObject
{
    private static readonly Regex E164Pattern = new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public static PhoneNumber Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = value.Trim().Replace(" ", "").Replace("-", "");
        if (!E164Pattern.IsMatch(normalized))
            throw new ArgumentException($"'{value}' is not a valid E.164 phone number.", nameof(value));
        return new PhoneNumber(normalized);
    }

    public static bool TryCreate(string value, out PhoneNumber? result)
    {
        try
        {
            result = Create(value);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public string Masked
    {
        get
        {
            if (Value.Length <= 6) return Value;
            var prefix = Value[..3];
            var suffix = Value[^4..];
            return $"{prefix}{"".PadLeft(Value.Length - 7, '*')}{suffix}";
        }
    }

    public static implicit operator string(PhoneNumber phone) => phone.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
