using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// A one-way hash of a bearer secret — refresh tokens and one-time codes. Storing
/// the hash rather than the secret means a database disclosure does not hand an
/// attacker usable credentials.
/// </summary>
public sealed class SecretHash : ValueObject
{
    public const int MaxLength = 256;

    public string Value { get; }

    private SecretHash(string value)
    {
        Value = value;
    }

    public static SecretHash Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        Guard.Against.MaxLength(value, MaxLength, nameof(value));
        return new SecretHash(value);
    }

    /// <summary>
    /// Fixed-time comparison so that matching a candidate hash cannot be timed.
    /// </summary>
    public bool Matches(SecretHash other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FixedTimeEquals(Value, other.Value);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var difference = (uint)left.Length ^ (uint)right.Length;
        for (var i = 0; i < left.Length && i < right.Length; i++)
        {
            difference |= (uint)left[i] ^ (uint)right[i];
        }

        return difference == 0;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => "***";
}
