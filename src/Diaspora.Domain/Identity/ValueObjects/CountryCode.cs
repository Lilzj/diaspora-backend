using System.Text.RegularExpressions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// An ISO 3166-1 alpha-2 country code. Used for both nationality and current
/// country of residence — the two that, together, define a diaspora relationship.
/// </summary>
public sealed class CountryCode : ValueObject
{
    private static readonly Regex Alpha2Pattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

    public string Value { get; }

    private CountryCode(string value)
    {
        Value = value;
    }

    public static CountryCode Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = value.Trim().ToUpperInvariant();
        if (!Alpha2Pattern.IsMatch(normalized))
            throw new ArgumentException($"'{value}' is not a valid ISO 3166-1 alpha-2 country code.", nameof(value));
        return new CountryCode(normalized);
    }

    public static bool TryCreate(string? value, out CountryCode? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim().ToUpperInvariant();
        if (!Alpha2Pattern.IsMatch(normalized)) return false;

        result = new CountryCode(normalized);
        return true;
    }

    public static implicit operator string(CountryCode code) => code.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
