using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Signs a neighbourhood profile off for publication.
/// <para>
/// A narrower permission than writing, deliberately: publishing prose about the places members live
/// under the platform name is a different act from drafting it. It also stamps the review date,
/// which is what lets the platform find profiles quietly describing a place that has changed.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.NeighborhoodApprove)]
[Auditable]
public sealed record ApproveNeighborhoodProfileCommand : ICommand<NeighborhoodProfileResponse>
{
    /// <summary>The Location area whose profile is being approved.</summary>
    public required Guid AreaId { get; init; }
}
