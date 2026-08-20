using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// What an event is called.
/// <para>
/// Whitespace is collapsed rather than merely trimmed, and that is what makes duplicate detection
/// work: "Independence  Day   Party" and "Independence Day Party" are the same event posted twice,
/// and a repository comparing raw strings would see two. Duplicate events are the single most
/// common complaint about any community calendar — four committee members each posting the same
/// festival — so this is not a tidiness rule.
/// </para>
/// </summary>
public sealed class CommunityLifeTitle : ValueObject
{
    private CommunityLifeTitle(string value) => Value = value;

    private CommunityLifeTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<CommunityLifeTitle> Create(string? value)
    {
        var normalized = Normalize(value);

        return normalized is null
            ? Result.Failure<CommunityLifeTitle>(CommunityLifeErrors.TitleInvalid)
            : Result.Success(new CommunityLifeTitle(normalized));
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

        return collapsed.Length is >= CommunityLifeLimits.MinTitle
            and <= CommunityLifeLimits.MaxTitle
            ? collapsed
            : null;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
