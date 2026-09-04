using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// One placement a campaign booked.
/// <para>
/// <b>A reference, not a copy.</b> The <see cref="Placements.Placement"/> aggregate stays exactly
/// where it is and keeps owning its own window, status, pause state and priority weight — this is
/// the join that says which campaign paid for it, plus the one thing a campaign needs that a
/// placement has no business knowing: which kind of slot it bought, so the performance read can
/// group by it without loading every placement.
/// </para>
/// <para>
/// <b>Owned by the campaign, so it goes when the campaign goes.</b> A placement outlives its
/// campaign — it is a financial record — but this join does not.
/// </para>
/// </summary>
public sealed class AdCampaignPlacement : ValueObject
{
    private AdCampaignPlacement(PlacementId placementId, PlacementKind kind)
    {
        PlacementId = placementId;
        Kind = kind;
    }

    private AdCampaignPlacement() { }

    public PlacementId PlacementId { get; private set; } = default!;

    /// <summary>Where the slot is. Copied so a per-kind rollup needs no join.</summary>
    public PlacementKind Kind { get; private set; }

    public static AdCampaignPlacement For(PlacementId placementId, PlacementKind kind)
    {
        ArgumentNullException.ThrowIfNull(placementId);

        return new AdCampaignPlacement(placementId, kind);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PlacementId;
    }
}
