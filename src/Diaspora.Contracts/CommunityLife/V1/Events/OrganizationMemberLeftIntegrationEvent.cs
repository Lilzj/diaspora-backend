using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// Somebody stopped belonging to an organization.
/// <para>
/// <b>Refusals are never published, and this is the reason the departure contract is separate from
/// the membership status stream.</b> A rejected request is between the applicant and the
/// organization's administrators; broadcasting it would tell every subscriber that somebody asked to
/// join a mosque and was turned down.
/// </para>
/// <para>
/// <see cref="WasRemoved"/> distinguishes leaving from being removed, because the two mean
/// different things to a consumer computing participation, and neither carries the reason text: a
/// removal reason is written for the organization's own record.
/// </para>
/// </summary>
public sealed record OrganizationMemberLeftIntegrationEvent : IntegrationEvent
{
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid MemberUserId { get; init; }

    /// <summary>Whether an administrator removed them, as opposed to their walking away.</summary>
    public required bool WasRemoved { get; init; }

    /// <summary>Whether the membership simply lapsed. Neither a departure nor a removal.</summary>
    public required bool WasLapsed { get; init; }

    public required int MemberCount { get; init; }

    public required DateTimeOffset LeftAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// Carried for the lapse above all: <see cref="WasLapsed"/> is the departure nobody chose,
    /// and it is the one an administrator would otherwise only discover by noticing the count
    /// move. A removal they performed themselves warrants nothing, which the routing rules
    /// decide rather than this contract.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
