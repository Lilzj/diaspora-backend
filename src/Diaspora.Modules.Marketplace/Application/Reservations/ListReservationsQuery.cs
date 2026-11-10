using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// The caller's reservations, as buyer or as seller.
/// <para>
/// Always scoped to the caller. A reservation says where somebody is going to be and when, which is
/// the most sensitive thing this module records about anyone.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ReservationManage)]
public sealed record ListReservationsQuery : IQuery<CursorPagedResult<ReservationResponse>>
{
    /// <summary><c>Buying</c> or <c>Selling</c>. Buying when omitted.</summary>
    public string? Side { get; init; }

    /// <summary>Only reservations still holding stock. True on the dashboard.</summary>
    public bool ActiveOnly { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
