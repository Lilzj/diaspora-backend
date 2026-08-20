using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// What the guide is called.
/// <para>
/// A value object rather than a string because the same rule is enforced at creation, on
/// metadata update, on version restore and in the search projection, and a rule enforced in
/// four places is a rule enforced inconsistently. Whitespace is collapsed rather than merely
/// trimmed: "How to  open a  bank account" and "How to open a bank account" are the same guide,
/// and leaving them distinct produces two search entries that differ invisibly.
/// </para>
/// </summary>
public sealed class GuideTitle : ValueObject
{
    private GuideTitle(string value) => Value = value;

    private GuideTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<GuideTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<GuideTitle>(GuideErrors.TitleInvalid);
        }

        var normalized = Normalize(value);

        return normalized.Length is < GuideLimits.MinTitle or > GuideLimits.MaxTitle
            ? Result.Failure<GuideTitle>(GuideErrors.TitleInvalid)
            : Result.Success(new GuideTitle(normalized));
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Trims, then collapses every internal whitespace run to a single space.</summary>
    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
