using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A moderator published a neighbourhood insight.
/// <para>
/// <b>This one does carry the text, because a moderator has read it.</b> That asymmetry with the
/// submission contract is the whole moderation model expressed on the bus: unreviewed claims about
/// the places members live do not leave the module, and reviewed ones are ordinary content.
/// </para>
/// <para>
/// Rejections are deliberately not published at all. A refused insight existence is between its
/// author and the moderator, and broadcasting it would tell every subscriber what a member tried
/// to say about somewhere.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightApprovedIntegrationEvent : IntegrationEvent
{
    public required Guid InsightId { get; init; }

    public required Guid AreaId { get; init; }

    public required Guid AuthorId { get; init; }

    public required string Category { get; init; }

    /// <summary>What the member said. Reviewed before it reached this contract.</summary>
    public required string Body { get; init; }

    /// <summary>A short chip, where the author gave one.</summary>
    public string? Label { get; init; }

    public required Guid ApprovedBy { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }
}
