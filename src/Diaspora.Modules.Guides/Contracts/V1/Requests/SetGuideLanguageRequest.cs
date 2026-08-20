namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Corrects the language the guide is written in.</summary>
public sealed record SetGuideLanguageRequest
{
    /// <summary>Lower-case ISO 639-1 two-letter code.</summary>
    public required string Language { get; init; }

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
