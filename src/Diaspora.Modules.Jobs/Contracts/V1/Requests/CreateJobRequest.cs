namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Opens a new job posting as a draft.
/// <para>
/// Deliberately thin. A title, a description, a category, a type and a location are the whole
/// cost of starting — everything richer arrives through later edits against the same aggregate,
/// because a creation form demanding the finished advert is one recruiters abandon and then post
/// the job somewhere else.
/// </para>
/// </summary>
public sealed record CreateJobRequest
{
    /// <summary>The employer to post under. The caller must be a recruiter there or above.</summary>
    public required Guid EmployerId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>The card line. Derived from the description when omitted.</summary>
    public string? Summary { get; init; }

    /// <summary>A requested deep-link handle. One is derived from the title when omitted.</summary>
    public string? Slug { get; init; }

    /// <summary>A key from the employment-domain catalogue.</summary>
    public required string CategoryKey { get; init; }

    /// <summary><c>FullTime</c>, <c>Contract</c>, <c>Internship</c> and so on.</summary>
    public required string JobType { get; init; }

    /// <summary><c>Entry</c>, <c>Mid</c>, <c>Senior</c> and so on.</summary>
    public required string ExperienceLevel { get; init; }

    /// <summary>
    /// Upper-case ISO 3166-1 alpha-2. Required even for a fully remote role: "remote" always
    /// means remote within some jurisdiction, and a posting that does not say which is one a
    /// candidate cannot act on.
    /// </summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>Location's area identifier, when the posting is scoped below country level.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>
    /// The posting's pin, in WGS84 degrees (-90..90). Optional; send both or neither.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>The posting's pin, in WGS84 degrees (-180..180). Optional; send both or neither.</summary>
    public double? Longitude { get; init; }

    /// <summary><c>OnSite</c>, <c>Hybrid</c> or <c>Remote</c>. Defaults to on site.</summary>
    public string? WorkplaceType { get; init; }

    /// <summary>The employer's claim that it will sponsor a work visa. Never verified.</summary>
    public bool OffersVisaSponsorship { get; init; }

    public bool OffersRelocationAssistance { get; init; }

    /// <summary>"Simple" for short one-off work answered with "I'm interested"; absent or "Standard" otherwise.</summary>
    public string? PostingMode { get; init; }

    /// <summary>The day the work starts (simple jobs). Sent with a length, or not at all.</summary>
    public DateOnly? StartsOn { get; init; }

    /// <summary>How long the work lasts: 1–24 hours or 1–365 days.</summary>
    public int? LengthAmount { get; init; }

    /// <summary>"Hours" or "Days".</summary>
    public string? LengthUnit { get; init; }
}
