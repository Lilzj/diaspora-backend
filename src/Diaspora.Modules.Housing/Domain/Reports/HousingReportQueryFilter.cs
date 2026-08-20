using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Reports;

/// <summary>
/// How the moderation queue is narrowed.
/// <para>
/// Every use of this filter is behind the moderation permission. There is no member-facing read
/// path for reports at all - not even for a member's own, because a landlord who could enumerate
/// reports against their listing could work out who filed them.
/// </para>
/// </summary>
public sealed record HousingReportQueryFilter
{
    public HousingListingId? ListingId { get; init; }

    public LandlordProfileId? LandlordId { get; init; }

    public IReadOnlyCollection<HousingReportStatus>? Statuses { get; init; }

    public IReadOnlyCollection<HousingReportReason>? Reasons { get; init; }

    /// <summary>Only reports still waiting on somebody. The queue's default.</summary>
    public bool OpenOnly { get; init; }

    /// <summary>Scam allegations first. The queue's most consequential ordering.</summary>
    public bool ScamsFirst { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
