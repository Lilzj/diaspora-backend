using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// The language a guide is written in, as a lower-case ISO 639-1 two-letter code.
/// <para>
/// Two letters and not BCP-47, deliberately: a guide's language decides which translation a
/// reader is offered, and "en" versus "en-GB" is a distinction that would split one English
/// guide into regional near-duplicates nobody maintains. Regional nuance belongs in the
/// guide's location context, which is a separate axis.
/// </para>
/// </summary>
public sealed class GuideLanguage : ValueObject
{
    private GuideLanguage(string code) => Code = code;

    private GuideLanguage() { }

    public string Code { get; private set; } = string.Empty;

    public static Result<GuideLanguage> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<GuideLanguage>(GuideErrors.LanguageInvalid);
        }

        var candidate = code.Trim().ToLowerInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterLower)
            ? Result.Success(new GuideLanguage(candidate))
            : Result.Failure<GuideLanguage>(GuideErrors.LanguageInvalid);
    }

    public override string ToString() => Code;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}
