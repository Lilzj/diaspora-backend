namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>One language a provider serves customers in, and how well.</summary>
public sealed record ProviderLanguageResponse
{
    public required Guid LanguageId { get; init; }

    /// <summary>BCP-47, lower case. "en", "fr", "yo", "pt-br".</summary>
    public required string Code { get; init; }

    /// <summary>The <c>LanguageProficiency</c> name — <c>Fluent</c>, <c>Native</c>.</summary>
    public required string Proficiency { get; init; }

    public required bool IsPrimary { get; init; }
}
