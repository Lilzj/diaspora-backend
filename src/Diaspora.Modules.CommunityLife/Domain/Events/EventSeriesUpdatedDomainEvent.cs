using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when a series' template or rule changes.
/// <para>
/// <b><see cref="Scope"/> travels with it because the consequence differs entirely by scope.</b> An
/// edit to one occurrence affects the people coming to that occurrence; an edit to the rule affects
/// everybody holding a place at anything the rule still owns. A consumer sizing a notification needs
/// to know which before it reads anything back.
/// </para>
/// </summary>
public sealed record EventSeriesUpdatedDomainEvent : DomainEvent
{
    public required EventSeriesId EventSeriesId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public UserId? ChangedBy { get; init; }

    public required SeriesEditScope Scope { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>Whether the change moved occurrences in time or space. See the event equivalent.</summary>
    public bool IsMaterialChange { get; init; }

    /// <summary>How many already-materialised occurrences the edit moved.</summary>
    public int AffectedOccurrenceCount { get; init; }
}
