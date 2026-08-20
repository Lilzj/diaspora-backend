using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// A member's full name as supplied at registration. Diaspora members come from
/// naming cultures with wildly different structures, so the name is deliberately
/// kept as a single normalised string rather than forced into given/family parts.
/// </summary>
public sealed class FullName : ValueObject
{
    public const int MinLength = 2;
    public const int MaxLength = 120;

    public string Value { get; }

    private FullName(string value)
    {
        Value = value;
    }

    public static FullName Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = Normalize(value);
        Guard.Against.LengthRange(normalized, MinLength, MaxLength, nameof(value));
        return new FullName(normalized);
    }

    public static bool TryCreate(string? value, out FullName? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = Normalize(value);
        if (normalized.Length is < MinLength or > MaxLength) return false;

        result = new FullName(normalized);
        return true;
    }

    /// <summary>
    /// Collapses runs of whitespace so "  Amara   Okafor " and "Amara Okafor"
    /// are the same name.
    /// </summary>
    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The display initials, used for avatar placeholders.
    /// </summary>
    public string Initials
    {
        get
        {
            var parts = Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => string.Empty,
                1 => parts[0][..1].ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            };
        }
    }

    public static implicit operator string(FullName name) => name.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
