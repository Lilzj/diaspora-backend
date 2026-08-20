using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Diaspora.SharedKernel;

/// <summary>
/// The guard implementations behind <see cref="Guard.Against"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification =
        "These are reached through the Guard.Against singleton, which is what gives the " +
        "call sites their sentence-like shape. Static members cannot be invoked through an " +
        "instance, so satisfying the rule would mean abandoning the entire fluent API.")]
public sealed class GuardClause
{
    internal GuardClause() { }

    public T Null<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    public string NullOrEmpty(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        return value;
    }

    public string NullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
        return value;
    }

    public IReadOnlyCollection<T> NullOrEmpty<T>(IReadOnlyCollection<T>? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null || value.Count == 0)
            throw new ArgumentException("Collection cannot be null or empty.", paramName);
        return value;
    }

    public T OutOfRange<T>(T value, string paramName, T min, T max)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Value must be between {min} and {max}.");
        return value;
    }

    public int Negative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        return value;
    }

    public long Negative(long value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        return value;
    }

    public decimal Negative(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        return value;
    }

    public Guid Empty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Guid cannot be empty.", paramName);
        return value;
    }

    public string MaxLength(string value, int maxLength, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Length > maxLength)
            throw new ArgumentException($"String length ({value.Length}) exceeds maximum ({maxLength}).", paramName);
        return value;
    }

    public string MinLength(string value, int minLength, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Length < minLength)
            throw new ArgumentException($"String length ({value.Length}) is below minimum ({minLength}).", paramName);
        return value;
    }

    public string LengthRange(string value, int min, int max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Length < min || value.Length > max)
            throw new ArgumentException($"String length ({value.Length}) must be between {min} and {max}.", paramName);
        return value;
    }

    public string InvalidFormat(string value, System.Text.RegularExpressions.Regex pattern, string formatName, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!pattern.IsMatch(value))
            throw new ArgumentException($"Value does not match the expected format: {formatName}.", paramName);
        return value;
    }

    public T Default<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            throw new ArgumentException("Value cannot be the default value.", paramName);
        return value;
    }
}
