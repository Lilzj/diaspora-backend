using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// The featured carousel — the guides staff chose to put in front of everybody.
/// <para>
/// A separate query rather than a flag on discovery, because it is a different editorial
/// promise: featuring is a staff decision about what a member should read first, and it is worth
/// being able to change how that shelf is built without touching search.
/// </para>
/// </summary>
public sealed record ListFeaturedGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    /// <summary>Narrows the shelf to one country. Null features globally.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Opaque. From a previous page's <c>nextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
