using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a moderator approves, rejects or retires an insight.
/// <para>
/// One event for all three outcomes, and the relay publishes an approval outward while keeping a
/// rejection inside the module. A rejected insight's existence is between its author and the
/// moderator; broadcasting it would tell the bus what a member tried to say about a place.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightDecidedDomainEvent : DomainEvent
{
    public required NeighborhoodInsightId InsightId { get; init; }

    /// <summary>Null for an insight written at a point rather than about a catalogue neighbourhood.</summary>
    public NeighborhoodProfileId? ProfileId { get; init; }

    /// <summary>Null for an insight written at a point (14 Oct 2026).</summary>
    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public required UserId AuthorId { get; init; }

    public required NeighborhoodInsightCategory Category { get; init; }

    public required NeighborhoodInsightStatus PreviousStatus { get; init; }

    public required NeighborhoodInsightStatus CurrentStatus { get; init; }

    public UserId? DecidedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}
