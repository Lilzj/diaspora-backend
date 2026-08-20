namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One country on the "browse by country" grid.
/// <para>
/// Deliberately not the full directory. A grid of a hundred and ninety-five countries that each
/// carried their entries, featured shelf and statistics would be several thousand rows to render a
/// list of names — the same card-versus-document split B8 makes between its guide card and its
/// guide response, applied one level up.
/// </para>
/// </summary>
public sealed record CountryDirectorySummaryResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Already localized.</summary>
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    public string? PrimaryLanguage { get; init; }

    public required bool IsPublished { get; init; }

    /// <summary>Navigation rows this country lists. What a grid tile shows as a subtitle.</summary>
    public required int CategoryCount { get; init; }

    /// <summary>Published guides for this country.</summary>
    public required int GuideCount { get; init; }

    /// <summary>
    /// How much interest this country has drawn, as a directory view count.
    /// <para>
    /// On the card because an explore grid sorted by it is the single most useful default ordering
    /// there is: it puts the countries people are actually asking about first, which for a diaspora
    /// platform is a genuinely different list from the largest countries.
    /// </para>
    /// </summary>
    public required long DirectoryViews { get; init; }
}
