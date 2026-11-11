using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Dashboards;

/// <summary>
/// The editorial view of the neighbourhood estate.
/// <para>
/// Built for the people who keep the profiles honest. <b>A profile nobody has looked at since a
/// tram line opened is quietly wrong</b>, and a directory of quietly wrong prose about the places
/// members live is worse than an empty one — so what this screen surfaces is what is unwritten,
/// what is unreviewed and what is waiting.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.NeighborhoodManage)]
public sealed record GetNeighborhoodDashboardQuery : IQuery<NeighborhoodDashboardResponse>
{
    /// <summary>Narrows the estate to one country. Editorial teams are usually regional.</summary>
    public string? CountryCode { get; init; }
}
