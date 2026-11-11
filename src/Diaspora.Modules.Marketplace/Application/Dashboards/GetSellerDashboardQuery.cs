using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Dashboards;

/// <summary>
/// Everything a seller needs on opening the app, in one call.
/// <para>
/// Assembled server-side deliberately: a client that made six calls to build this screen would
/// render it in six stages on a slow connection, and would be six chances to get the authorisation
/// of each wrong.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.SellerManage)]
public sealed record GetSellerDashboardQuery : IQuery<SellerDashboardResponse>;
