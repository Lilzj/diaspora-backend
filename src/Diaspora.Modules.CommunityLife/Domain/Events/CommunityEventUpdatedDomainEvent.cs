using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when an event's content changes. Carries what changed, not the values.</summary>
public sealed record CommunityEventUpdatedDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public UserId? ChangedBy { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>
    /// Whether the change moved the event in time or space.
    /// <para>
    /// Called out separately because it is the one kind of edit that has to reach everybody who
    /// said they were coming. An organizer fixing a typo does not; an organizer moving a wedding
    /// reception to a different hall very much does.
    /// </para>
    /// </summary>
    public bool IsMaterialChange { get; init; }
}
