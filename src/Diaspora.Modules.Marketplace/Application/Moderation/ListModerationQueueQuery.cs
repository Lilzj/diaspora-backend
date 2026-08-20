using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>The marketplace moderation queue.</summary>
[Authorize(Permission = MarketplacePermissions.Moderate)]
public sealed record ListModerationQueueQuery : IQuery<CursorPagedResult<ListingReportResponse>>
{
    /// <summary><c>Open</c>, <c>Reviewing</c>, <c>Actioned</c> or <c>Dismissed</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Narrow to one reason. Fraud first is how a real queue is worked.</summary>
    public string? Reason { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
