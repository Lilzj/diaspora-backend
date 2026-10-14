using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a member says something about a neighbourhood.
/// <para>
/// <b>Submission is not publication.</b> Nothing renders on the strength of this event: the
/// insight is invisible to everybody but its author and the moderation queue until an approval
/// follows. A consumer that indexed on submission would publish unreviewed claims about the places
/// members live.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightSubmittedDomainEvent : DomainEvent
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

    public required DateTimeOffset SubmittedAt { get; init; }
}
