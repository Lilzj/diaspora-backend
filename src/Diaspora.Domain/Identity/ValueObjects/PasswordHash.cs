using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.ValueObjects;

/// <summary>
/// An opaque password hash. The domain never sees plaintext passwords and never
/// interprets the hash format — algorithm choice and verification belong to the
/// infrastructure adapter behind <c>IPasswordHasher</c>.
/// </summary>
public sealed class PasswordHash : ValueObject
{
    public const int MaxLength = 512;

    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    public static PasswordHash Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        Guard.Against.MaxLength(value, MaxLength, nameof(value));
        return new PasswordHash(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Deliberately does not render the hash — it must never leak into logs.
    /// </summary>
    public override string ToString() => "***";
}
