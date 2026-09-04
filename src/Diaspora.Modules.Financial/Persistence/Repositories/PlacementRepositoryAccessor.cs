using Diaspora.Modules.Financial.Application.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// One read of one placement, for the performance handler.
/// <para>
/// A narrow seam over the placement repository rather than the repository itself, so a read-only
/// handler cannot quietly grow the ability to cancel, renew or re-price what it is reporting on.
/// </para>
/// </summary>
public sealed class PlacementRepositoryAccessor : IPlacementRepositoryAccessor
{
    private readonly IPlacementRepository _placements;

    public PlacementRepositoryAccessor(IPlacementRepository placements) => _placements = placements;

    public Task<Placement?> FindAsync(
        PlacementId placementId, CancellationToken cancellationToken = default) =>
        _placements.FindAsync(placementId, cancellationToken);
}
