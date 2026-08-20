namespace Diaspora.Modules.Financial.Extensibility.Campaigns;

/// <summary>
/// Whether one caller may run fundraising on one organization's behalf.
/// <para>
/// <b>The register's rule: <c>canManageCampaigns</c> for an organization campaign,
/// <c>AccessTier.Verified</c> for a personal one.</b> The second is B15's own — the giving
/// permission already sits at the verified rung and the campaign write is gated on it. The first is
/// a capability inside somebody else's body, decided by the module that owns the role edge, and
/// asked for here rather than reimplemented.
/// </para>
/// <para>
/// <b>Deny by default.</b> With nothing registered, every organization campaign is refused and
/// personal ones are unaffected. That is the safe direction: the failure mode of an unimplemented
/// seam must be "you may not", never "nobody said no". An integration test creates a real
/// organization campaign end to end, because a seam that is registered, architecture-tested and
/// never actually reached is the exact defect B15.1 found in the webhook processor.
/// </para>
/// </summary>
public interface ICampaignOrganizerAuthority
{
    /// <summary>Whether this caller holds the campaign-management capability inside this body.</summary>
    Task<bool> CanManageCampaignsAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The organization's display name, for the campaign's organizer label.
    /// <para>
    /// Null when it cannot be resolved, and the campaign then carries the member's own name — a
    /// campaign with a blank organizer is worse than one attributed to the person who created it.
    /// </para>
    /// </summary>
    Task<string?> DescribeAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the platform does when nothing implements <see cref="ICampaignOrganizerAuthority"/>.
/// <para>
/// Refuses every organization, resolves no name. Registered by B15 itself so the seam is never
/// missing from the container — an absent registration would be a startup failure on a route rather
/// than a refusal on one, and the refusal is the honest answer.
/// </para>
/// </summary>
public sealed class DenyingCampaignOrganizerAuthority : ICampaignOrganizerAuthority
{
    public Task<bool> CanManageCampaignsAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<string?> DescribeAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
