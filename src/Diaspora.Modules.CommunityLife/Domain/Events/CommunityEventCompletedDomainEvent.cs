using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an event finishes.
/// <para>
/// Carries the attendance figures, because this is the moment they stop moving and the moment every
/// downstream reputation and analytics consumer wants them.
/// </para>
/// </summary>
public sealed record CommunityEventCompletedDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required string CategoryKey { get; init; }

    public required int GoingCount { get; init; }

    public required int CheckedInCount { get; init; }

    public required int NoShowCount { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
