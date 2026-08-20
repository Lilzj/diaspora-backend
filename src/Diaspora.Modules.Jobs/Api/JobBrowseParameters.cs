using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// The subset of the search filters that fits in a query string.
/// <para>
/// <b>A deliberate subset, not the whole request.</b> The full filter set has list-valued
/// members that do not round-trip cleanly through a URL, and a browse link that carried every
/// one of them would be unreadable and unshareable. What is here is what a link actually needs
/// to carry: a country, a category, remote-only, sponsorship-only, and a sort.
/// </para>
/// <para>
/// A client that needs the rest posts to <c>/search</c>.
/// </para>
/// </summary>
public sealed record JobBrowseParameters
{
    [FromQuery(Name = "q")]
    public string? Query { get; init; }

    [FromQuery(Name = "country")]
    public string? CountryCode { get; init; }

    [FromQuery(Name = "city")]
    public string? CityName { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved by B4.</summary>
    [FromQuery(Name = "bounds")]
    public string? Bounds { get; init; }

    [FromQuery(Name = "area")]
    public Guid? AreaId { get; init; }

    [FromQuery(Name = "category")]
    public string? CategoryKey { get; init; }

    [FromQuery(Name = "tag")]
    public string? Tag { get; init; }

    [FromQuery(Name = "workplace")]
    public string? WorkplaceType { get; init; }

    [FromQuery(Name = "type")]
    public string? JobType { get; init; }

    [FromQuery(Name = "experience")]
    public string? MinimumExperience { get; init; }

    // Nullable, and not because absence means anything. Minimal APIs treat a non-nullable value
    // type as a required parameter, so `bool` here would turn the plainest browse link in the
    // product — /api/v1/jobs?country=DE — into a 400. The filters are opt-in; absent is off.
    [FromQuery(Name = "sponsored")]
    public bool? VisaSponsorshipOnly { get; init; }

    [FromQuery(Name = "verified")]
    public bool? VerifiedEmployersOnly { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    public SearchJobsRequest ToRequest() => new()
    {
        Query = Query,
        CountryCode = CountryCode,
        CityName = CityName,
        Bounds = Bounds,
        AreaId = AreaId,
        CategoryKey = CategoryKey,
        Tag = Tag,
        WorkplaceTypes = WorkplaceType is null ? null : [WorkplaceType],
        JobTypes = JobType is null ? null : [JobType],
        MinimumExperience = MinimumExperience,
        VisaSponsorshipOnly = VisaSponsorshipOnly ?? false,
        VerifiedEmployersOnly = VerifiedEmployersOnly ?? false,
        Sort = Sort,
        Cursor = Cursor,
        Limit = Limit ?? 20
    };
}
