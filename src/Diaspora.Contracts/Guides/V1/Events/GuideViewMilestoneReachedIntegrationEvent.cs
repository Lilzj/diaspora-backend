using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A guide's total views crossed a power-of-ten milestone.
/// <para>
/// Views are aggregated inside the module; nothing crosses the boundary per view. This fires only
/// when the running total crosses 10, 100, 1&#160;000, 10&#160;000 and so on — the cadence at
/// which "this guide is being read" is news a notification or ranking consumer can act on
/// without drowning. A consumer wanting exact counts reads the analytics surface; this event is
/// the headline, not the ledger.
/// </para>
/// </summary>
public sealed record GuideViewMilestoneReachedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>The total at the moment the milestone was crossed — at least the milestone, possibly past it.</summary>
    public required long TotalViews { get; init; }

    public required DateTimeOffset MilestoneAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// The recipient the milestone cadence was chosen for. "Your guide has been read a thousand
    /// times" is worth sending precisely because it arrives rarely, and it is worth nothing at
    /// all if the payload cannot say whose guide it is.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
