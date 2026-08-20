namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Replaces the whole language list.
/// <para>
/// Wholesale rather than per-language, because the client's editor is a multi-select and a
/// person picks a set and saves. A per-language API would make the common case several round
/// trips and a partial-failure story.
/// </para>
/// </summary>
public sealed record ProviderLanguagesRequest
{
    public required IReadOnlyList<ProviderLanguageEntry> Languages { get; init; }
}
