using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One language a provider serves customers in, and how well.
/// <para>
/// <b>One of the most valuable fields in the module for the platform this is.</b> "Do they speak
/// Twi?" is a real question a member asks before choosing a solicitor, and the client's
/// <c>mockLanguages</c> catalogue (English, Français, العربية, Português, Kiswahili, Hausa,
/// Yorùbá, Igbo, አማርኛ) exists precisely because somebody understood that — but nothing in the
/// client attaches those languages to a provider. This is the join that was missing.
/// </para>
/// <para>
/// The code is a BCP-47 primary subtag, lower case, validated for shape and not against a
/// closed list. A closed list would need a release to add a language, and the languages this
/// platform will need are exactly the ones a closed list assembled in London would omit.
/// </para>
/// </summary>
public sealed class ProviderLanguage : Entity<ProviderLanguageId>
{
    public const int MinCodeLength = 2;

    public const int MaxCodeLength = 12;

    private ProviderLanguage(ProviderLanguageId id, string code, LanguageProficiency proficiency)
        : base(id)
    {
        Code = code;
        Proficiency = proficiency;
    }

    private ProviderLanguage() { }

    /// <summary>BCP-47, lower case. "en", "fr", "sw", "yo", "pt-br".</summary>
    public string Code { get; private set; } = string.Empty;

    public LanguageProficiency Proficiency { get; private set; } = LanguageProficiency.Conversational;

    /// <summary>Whether this is the language the provider prefers to be approached in.</summary>
    public bool IsPrimary { get; private set; }

    public static Result<ProviderLanguage> Create(
        ProviderLanguageId id,
        string? code,
        LanguageProficiency proficiency = LanguageProficiency.Conversational,
        bool isPrimary = false)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalized = Normalize(code);

        return normalized is null
            ? Result.Failure<ProviderLanguage>(ProviderErrors.LanguageCodeInvalid)
            : Result.Success(new ProviderLanguage(id, normalized, proficiency) { IsPrimary = isPrimary });
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    public void SetProficiency(LanguageProficiency proficiency) => Proficiency = proficiency;

    /// <summary>
    /// Accepts a primary subtag with optional region or script subtags — "en", "pt-BR",
    /// "zh-Hans" — and normalises to lower case so "PT-br" and "pt-BR" are one language.
    /// </summary>
    private static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var candidate = code.Trim().ToLowerInvariant();

        if (candidate.Length is < MinCodeLength or > MaxCodeLength)
        {
            return null;
        }

        var segments = candidate.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments[0].Length is < 2 or > 3)
        {
            return null;
        }

        return segments.All(segment => segment.All(char.IsAsciiLetterOrDigit)) ? candidate : null;
    }
}
