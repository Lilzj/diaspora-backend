using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>The caller's watchlist.</summary>
[Authorize(Permission = MarketplacePermissions.SavedManage)]
public sealed record ListSavedListingsQuery : IQuery<CursorPagedResult<SavedListingResponse>>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
