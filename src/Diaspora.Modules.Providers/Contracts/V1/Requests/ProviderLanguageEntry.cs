namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>One language a provider serves customers in, and how well.</summary>
public sealed record ProviderLanguageEntry
{
    /// <summary>BCP-47, any case. "en", "fr", "yo", "pt-BR".</summary>
    public required string Code { get; init; }

    /// <summary>The <c>LanguageProficiency</c> name. Defaults to <c>Conversational</c>.</summary>
    public string? Proficiency { get; init; }

    /// <summary>The language the provider prefers to be approached in.</summary>
    public bool IsPrimary { get; init; }
}
