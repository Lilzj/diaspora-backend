using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Everything published about one country.
/// <para>
/// The jurisdiction browse, and the one most of this platform's knowledge is actually organised
/// by: procedures are national far more often than they are anything else, and a member who has
/// moved wants the shelf for where they now live.
/// </para>
/// </summary>
public sealed record ListGuidesByCountryQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    /// <summary>ISO 3166-1 alpha-2. Case-insensitive on the way in.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Narrows within the country. Null browses the whole catalogue.</summary>
    public string? CategoryKey { get; init; }

    public string? Language { get; init; }

    /// <summary>A sort key — <c>newest</c>, <c>most-helpful</c>, <c>recently-updated</c>, <c>popular</c>, <c>trending</c>.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
