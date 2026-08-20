using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// A language a candidate speaks, and how well.
/// <para>
/// A lower-case ISO 639-1 code and not BCP-47: an employer asking for German does not mean
/// Austrian German, and splitting the two would make a candidate invisible to a posting they
/// are perfectly qualified for.
/// </para>
/// </summary>
public sealed class CandidateLanguage
{
    private CandidateLanguage(string code, LanguageProficiency proficiency, int order)
    {
        Code = code;
        Proficiency = proficiency;
        Order = order;
    }

    private CandidateLanguage() { }

    /// <summary>Lower-case ISO 639-1.</summary>
    public string Code { get; private set; } = string.Empty;

    public LanguageProficiency Proficiency { get; private set; }

    public int Order { get; private set; }

    /// <summary>Normalises a language code, or null when it is not a valid two-letter code.</summary>
    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var candidate = code.Trim().ToLowerInvariant();

        return candidate.Length == 2 && candidate.All(char.IsAsciiLetterLower) ? candidate : null;
    }

    internal static CandidateLanguage? Create(
        string? code, LanguageProficiency proficiency, int order)
    {
        var normalized = NormalizeCode(code);

        return normalized is null || !Enum.IsDefined(proficiency)
            ? null
            : new CandidateLanguage(normalized, proficiency, order);
    }
}
