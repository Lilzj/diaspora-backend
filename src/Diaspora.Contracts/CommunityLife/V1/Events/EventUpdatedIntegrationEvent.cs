using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An event's content changed.
/// <para>
/// <b>B12.1, and it closes a gap that had been open since B12 shipped.</b> The aggregate has raised
/// a domain event on every content change from the beginning; nothing translated it, so no consumer
/// outside the module could learn that an event had been edited. A saved search kept a stale title,
/// a notification could not say "the venue moved", and an automation platform had nothing to trigger
/// on. The contract was the missing half, not the fact.
/// </para>
/// <para>
/// <b><see cref="ChangedFields"/> names what moved and not what it moved to.</b> A consumer that
/// needs the new values reads the event; one that only needs to decide whether to act — most of them
/// — decides from this alone, and the payload cannot leak a field the reader is not allowed to see.
/// </para>
/// <para>
/// <b><see cref="IsMaterialChange"/> is the field to switch on.</b> It is true when the event moved
/// in time or space, which is the only kind of edit that has to reach everybody who said they were
/// coming. Notifying on a corrected typo as well is how a platform trains people to ignore it.
/// </para>
/// </summary>
public sealed record EventUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>The repeating event this date belongs to, when it belongs to one.</summary>
    public Guid? SeriesId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    /// <summary>Where it now starts. What a reminder already scheduled has to be rebased against.</summary>
    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required string TimeZoneId { get; init; }

    /// <summary>Property names on the aggregate — <c>Schedule</c>, <c>Venue</c>, <c>Title</c>.</summary>
    public IReadOnlyList<string> ChangedFields { get; init; } = [];

    /// <summary>Whether the event moved in time or space. See the type summary.</summary>
    public required bool IsMaterialChange { get; init; }

    /// <summary>How many people were holding a place when it changed.</summary>
    public required int AffectedAttendeeCount { get; init; }

    public Guid? ChangedByUserId { get; init; }

    /// <summary>The member running it, so a consumer can reach them without asking B12 who they are.</summary>
    public Guid? OrganizerUserId { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Whether a stranger could find the event by searching as it now stands — 21 Oct 2026. Appended,
    /// never inserted; see the publication contract's note. False when unknown.
    /// </summary>
    public bool IsListedPublicly { get; init; }
}
