using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An organization left discovery — archived by its owners or suspended by a moderator.
/// <para>
/// One contract for both, with <see cref="Reason"/> telling them apart, because every consumer does
/// the same thing with either: remove it from the index. Splitting them would make each consumer
/// subscribe twice to run one code path.
/// </para>
/// </summary>
public sealed record OrganizationArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }

    public required string Slug { get; init; }

    /// <summary>The state it moved to — <c>Archived</c> or <c>Suspended</c>.</summary>
    public required string CurrentState { get; init; }

    /// <summary>Whether a moderator did it, as opposed to the organization's own owners.</summary>
    public required bool IsModerationAction { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>
    /// The organization's registered administrator — B13.1. The member who created it.
    /// <para>
    /// <b>The recipient for the suspension half of this contract.</b> A body removed from the
    /// directory by a moderator has to be told by the platform, and
    /// <see cref="IsModerationAction"/> is what separates that from its own owners archiving it.
    /// </para>
    /// <para>
    /// One named administrator, not the committee. Community Life models membership as a
    /// separate aggregate and a fan-out across it is a decision for the module that owns it —
    /// which is why this contract carries a person and not an audience.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the organization could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
