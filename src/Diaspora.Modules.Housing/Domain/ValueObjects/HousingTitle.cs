using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What a listing is called.
/// <para>
/// Whitespace is collapsed rather than merely trimmed, and that is what makes duplicate detection
/// work: "Two  bed   flat" and "Two bed flat" are the same advert posted twice, and a repository
/// comparing raw strings would see two.
/// </para>
/// </summary>
public sealed class HousingTitle : ValueObject
{
    private HousingTitle(string value) => Value = value;

    private HousingTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<HousingTitle> Create(string? value)
    {
        var normalized = Normalize(value);

        return normalized is null
            ? Result.Failure<HousingTitle>(HousingErrors.TitleInvalid)
            : Result.Success(new HousingTitle(normalized));
    }

    /// <summary>
    /// The comparable form: collapsed whitespace, trimmed, length-checked. Null when the value
    /// could never be a title. Exposed so the repository's duplicate check compares like with like.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = string.Join(
            ' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length is >= HousingLimits.MinTitle and <= HousingLimits.MaxTitle
            ? collapsed
            : null;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
