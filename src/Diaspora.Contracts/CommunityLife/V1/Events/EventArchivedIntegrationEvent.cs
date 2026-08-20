using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>An event was filed away, by its organizer or by a moderator.</summary>
public sealed record EventArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string Slug { get; init; }

    /// <summary>The state it moved to — <c>Archived</c>, or a moderation state.</summary>
    public required string CurrentState { get; init; }

    public required bool IsModerationAction { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>
    /// The member who organized it — B13.1.
    /// <para>
    /// The recipient for the half of this contract that is a moderation action. An event hidden
    /// or suspended by a moderator has vanished from every surface its organizer told people to
    /// look at, and <see cref="IsModerationAction"/> is what distinguishes that from the
    /// organizer filing away their own event.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the event could not be read.</para>
    /// </summary>
    public Guid? OrganizerUserId { get; init; }
}
