using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// Staff featured a guide, or stopped featuring it.
/// <para>
/// One event for both directions, distinguished by <see cref="Featured"/>, because a consumer
/// holds a single boolean either way — two event types would force every consumer to subscribe
/// twice to maintain one flag. Featuring is editorial curation, not a property of the content:
/// it changes ranking and home-page placement, never the guide itself.
/// </para>
/// </summary>
public sealed record GuideFeaturedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>True when the guide became featured; false when the feature was removed.</summary>
    public required bool Featured { get; init; }

    /// <summary>The staff member who made the call.</summary>
    public required Guid ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The member who wrote the guide — B13.1.
    /// <para>
    /// <b>The enrichment this contract most needed.</b> <see cref="ChangedBy"/> is the editor who
    /// made the call, and a consumer holding only that could congratulate the staff member on
    /// featuring somebody else's work. Featuring a guide is the clearest good news the knowledge
    /// surface produces and it belongs to its author.
    /// </para>
    /// <para>
    /// Present on both directions, because a contract whose payload changed shape with a boolean
    /// would be two contracts wearing one name. Whether an unfeaturing warrants a message is the
    /// routing rules' decision.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the guide could not be read.</para>
    /// </summary>
    public Guid? AuthorUserId { get; init; }
}
