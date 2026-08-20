namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One translation of a country directory's header. Returned only to curators.</summary>
public sealed record CountryDirectoryLocalizationResponse
{
    /// <summary>A normalized BCP-47 tag.</summary>
    public required string Locale { get; init; }

    public required string DisplayName { get; init; }

    public string? Summary { get; init; }
}
