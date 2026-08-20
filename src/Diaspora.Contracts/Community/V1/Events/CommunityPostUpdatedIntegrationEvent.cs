using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// A published post changed.
/// <para>
/// Carries the revision number so a consumer can discard a redelivery it has already applied.
/// Search re-indexes on this; a notification module should not — telling every reader a typo was
/// fixed is how a feed teaches members to mute it.
/// </para>
/// </summary>
public sealed record CommunityPostUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid PostId { get; init; }

    public required Guid EditedBy { get; init; }

    public required int RevisionNumber { get; init; }

    public required bool IsMarkedEdited { get; init; }
}
