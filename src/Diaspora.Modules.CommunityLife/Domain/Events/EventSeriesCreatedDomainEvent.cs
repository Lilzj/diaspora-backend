using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when a recurring series is drafted.</summary>
public sealed record EventSeriesCreatedDomainEvent : DomainEvent
{
    public required EventSeriesId EventSeriesId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>The rule in words, as the server renders it. See <c>RecurrenceRule.Describe</c>.</summary>
    public required string RecurrenceDescription { get; init; }

    public required DateTimeOffset FirstStartsAt { get; init; }
}
