using System.Text.RegularExpressions;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class EmailAddress : ValueObject
{
    private static readonly Regex EmailPattern = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        var normalized = value.Trim().ToLowerInvariant();
        if (!EmailPattern.IsMatch(normalized))
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
        return new EmailAddress(normalized);
    }

    public static implicit operator string(EmailAddress email) => email.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
