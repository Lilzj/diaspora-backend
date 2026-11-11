using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// The caller's own listings, drafts and archived ones included.
/// <para>
/// The one listing query that widens the audience predicate, and it widens it only to listings the
/// caller's own seller profile owns.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ListingEditOwn)]
public sealed record ListMyListingsQuery : IQuery<CursorPagedResult<ListingCardResponse>>
{
    /// <summary>
    /// <c>Draft</c>, <c>Published</c>, <c>Active</c>, <c>Reserved</c>, <c>Sold</c>,
    /// <c>Expired</c> or <c>Archived</c>. Everything when omitted.
    /// </summary>
    public string? Status { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
