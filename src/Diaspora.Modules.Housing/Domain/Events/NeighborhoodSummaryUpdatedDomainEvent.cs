using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a neighbourhood's living summaries or liveability bands change.
/// <para>
/// These are living summaries rather than immutable facts, so this fires often and consumers must
/// treat it as "re-read the profile" rather than as a diff. <see cref="ChangedFields"/> is carried
/// so a cache can be narrowed, not so a consumer can reconstruct the prose.
/// </para>
/// </summary>
public sealed record NeighborhoodSummaryUpdatedDomainEvent : DomainEvent
{
    public required NeighborhoodProfileId ProfileId { get; init; }

    public required Guid AreaId { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }

    public UserId? ChangedBy { get; init; }

    /// <summary>Whether an approver signed the change off, as opposed to an editor drafting it.</summary>
    public required bool IsApproved { get; init; }
}
