namespace Diaspora.Modules.Financial.Extensibility.Entitlements;

/// <summary>
/// Reports how much of an entitlement a member has actually used.
/// <para>
/// <b>B15 owns the ceiling and never the consumption, and that split is the whole reason this seam
/// exists.</b> A plan says <c>listings.concurrent: 15</c>; only Marketplace knows how many listings
/// somebody has open, only Jobs knows how many posts a recruiter is running, and the Financial
/// Platform references neither. A counter B15 maintained itself would be a second copy of a number
/// somebody else owns — wrong the first time a listing is archived by a sweep this module never
/// hears about.
/// </para>
/// <para>
/// <b>Additive, and a missing provider is not an error.</b> A deployment without Marketplace
/// registered simply reports no counter for its keys, and the entitlement snapshot serves the
/// ceiling with a null consumption. The client draws no bar for a null — an empty bar reads as "none
/// used", which is a specific claim nobody made.
/// </para>
/// <para>
/// <b>Never call this to decide whether an action is allowed.</b> It answers "how many" for a
/// display; the vertical that owns the limit enforces it, at the moment of the action, against its
/// own data. A gate that asked B15 would be a gate that failed open whenever B15 was slow.
/// </para>
/// </summary>
public interface IEntitlementUsageProvider
{
    /// <summary>
    /// Which entitlement keys this provider can report on. Used to skip providers a member's plan
    /// does not touch, so a snapshot fans out to the two verticals that matter rather than to nine.
    /// </summary>
    IReadOnlyList<string> Keys { get; }

    /// <summary>
    /// Consumption for one member, for whichever of <paramref name="keys"/> this provider owns.
    /// <para>
    /// Returning nothing is a valid answer and means "not measured", which is different from zero.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EntitlementUsage>> UsageAsync(
        Guid memberUserId,
        IReadOnlyList<string> keys,
        DateTimeOffset periodStart,
        DateTimeOffset? periodEnd,
        CancellationToken cancellationToken = default);
}
