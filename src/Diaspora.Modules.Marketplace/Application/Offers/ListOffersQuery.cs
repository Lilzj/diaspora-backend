using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// The caller's negotiations, as buyer or as seller.
/// <para>
/// <b>Always scoped to the caller, and the handler is what makes that true.</b> A negotiation is
/// private to its two parties; a query that could be run without naming one of them would be a way
/// to read everybody's offers.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.OfferRespond)]
public sealed record ListOffersQuery : IQuery<CursorPagedResult<OfferCardResponse>>
{
    /// <summary>
    /// <c>Buying</c> or <c>Selling</c>. Buying when omitted, because most members are buyers.
    /// </summary>
    public string? Side { get; init; }

    /// <summary>Only negotiations still open. True on the dashboard.</summary>
    public bool OpenOnly { get; init; } = true;

    /// <summary>Only the ones waiting on the caller. What "your turn" is.</summary>
    public bool AwaitingMeOnly { get; init; }

    public Guid? ListingId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
