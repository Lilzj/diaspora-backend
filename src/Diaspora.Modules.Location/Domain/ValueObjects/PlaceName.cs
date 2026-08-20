using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// What a place is called: its English name, and optionally what the people who live there
/// call it.
/// <para>
/// The local name is not decoration. Diaspora members search for the place they left in the
/// language they left it in — someone looking for Köln will not type Cologne — and a
/// catalogue that only stores the anglicised name silently fails that search. Both names are
/// indexed; both match.
/// </para>
/// </summary>
public sealed class PlaceName : ValueObject
{
    public const int MaxLength = 160;

    /// <summary>The English name. Always present.</summary>
    public string Value { get; }

    /// <summary>The endonym, when it differs. Null when it does not.</summary>
    public string? LocalName { get; }

    private PlaceName(string value, string? localName)
    {
        Value = value;
        LocalName = localName;
    }

    public static Result<PlaceName> TryCreate(string? value, string? localName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PlaceName>(Error.Validation(
                LocationErrorCodes.AreaNotFound, "A place name is required."));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            return Result.Failure<PlaceName>(Error.Validation(
                LocationErrorCodes.AreaNotFound,
                $"A place name may not exceed {MaxLength} characters."));
        }

        var local = string.IsNullOrWhiteSpace(localName) ? null : localName.Trim();

        if (local is not null && local.Length > MaxLength)
        {
            local = local[..MaxLength];
        }

        // Storing the endonym when it equals the English name would double every index entry
        // for no additional match.
        if (string.Equals(local, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            local = null;
        }

        return Result.Success(new PlaceName(trimmed, local));
    }

    public static PlaceName Create(string value, string? localName = null)
    {
        var result = TryCreate(value, localName);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(value));
    }

    /// <summary>Whether either spelling contains the term. Case-insensitive.</summary>
    public bool Matches(string term) =>
        !string.IsNullOrWhiteSpace(term) &&
        (Value.Contains(term, StringComparison.OrdinalIgnoreCase) ||
         LocalName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return LocalName;
    }

    public override string ToString() => Value;
}
