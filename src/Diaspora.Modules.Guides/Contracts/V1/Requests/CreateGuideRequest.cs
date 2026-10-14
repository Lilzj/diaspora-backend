namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Opens a new guide as a draft.
/// <para>
/// Deliberately thin: a title, a category, a language and a jurisdiction are the whole cost of
/// starting, because knowledge is written over sessions and a creation form that demanded the
/// finished article would get abandoned drafts pasted into posts instead. Structure, tags,
/// media and references all arrive through later edits.
/// </para>
/// </summary>
public sealed record CreateGuideRequest
{
    public required string Title { get; init; }

    /// <summary>A requested deep-link handle. Omitted, one is derived from the title.</summary>
    public string? Slug { get; init; }

    /// <summary>A key from the guide category catalogue.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>Lower-case ISO 639-1 two-letter code.</summary>
    public required string Language { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Every guide has a jurisdiction.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's area identifier, when the guide is scoped below country level.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>
    /// The pin, in WGS84 degrees (-90..90). Optional; send both or neither.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>The pin, in WGS84 degrees (-180..180). Optional; send both or neither.</summary>
    public double? Longitude { get; init; }

    public string? Summary { get; init; }
}
