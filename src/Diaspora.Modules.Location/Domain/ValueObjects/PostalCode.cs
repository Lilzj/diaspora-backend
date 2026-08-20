using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A postal or ZIP code, normalised to upper case with internal whitespace collapsed.
/// <para>
/// Validated on length and character class only, never against a per-country pattern.
/// Postal formats are the classic example of a rule everyone believes they know and nobody
/// gets right — the UK alone has seven valid shapes, Ireland's Eircodes were invented in
/// 2015, and several countries have none at all. A regex per country in this file would
/// reject real addresses, and rejecting a real address is worse than storing an odd one.
/// </para>
/// </summary>
public sealed class PostalCode : ValueObject
{
    public const int MaxLength = 16;

    public string Value { get; }

    private PostalCode(string value)
    {
        Value = value;
    }

    public static Result<PostalCode> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PostalCode>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "A postal code is required."));
        }

        var normalised = string.Join(
            ' ',
            value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (normalised.Length > MaxLength)
        {
            return Result.Failure<PostalCode>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                $"A postal code may not exceed {MaxLength} characters."));
        }

        var wellFormed = normalised.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is ' ' or '-');

        return wellFormed
            ? Result.Success(new PostalCode(normalised))
            : Result.Failure<PostalCode>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                "A postal code may contain only letters, digits, spaces and hyphens."));
    }

    public static PostalCode Create(string value)
    {
        var result = TryCreate(value);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
