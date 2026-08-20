using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when a series moves through its lifecycle.
/// <para>
/// One event for all five destinations, for the reason the module already records about
/// organizations and events: publication, pausing, completion and cancellation are four routes to
/// the same consumer obligation — reconsider what you are showing — and a consumer that reacted to
/// all four would otherwise need four subscriptions.
/// </para>
/// </summary>
public sealed record EventSeriesStatusChangedDomainEvent : DomainEvent
{
    public required EventSeriesId EventSeriesId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required EventSeriesStatus PreviousStatus { get; init; }

    public required EventSeriesStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
