using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Dashboards;

/// <summary>
/// What a landlord sees when they open the platform.
/// <para>
/// Ordered by what needs doing rather than by what exists. <b>Unanswered viewing requests come
/// first, because a member is waiting on each one</b> and every hour of silence is an hour they
/// spend looking somewhere else — which is also the figure the landlord's own responsiveness score
/// is built from, so the screen and the incentive point the same way.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingEditOwn)]
public sealed record GetHousingDashboardQuery : IQuery<HousingDashboardResponse>;
