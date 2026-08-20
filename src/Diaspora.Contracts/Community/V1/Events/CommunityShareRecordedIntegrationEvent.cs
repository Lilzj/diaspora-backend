using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>Content was passed along, inside or outside the platform.</summary>
public sealed record CommunityShareRecordedIntegrationEvent : IntegrationEvent
{
    public required Guid ShareId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid SharedBy { get; init; }

    /// <summary><c>internal</c>, <c>external</c>, <c>quote</c> or <c>reshare</c>.</summary>
    public required string Kind { get; init; }

    public Guid? ContentAuthorId { get; init; }

    public Guid? RecipientId { get; init; }
}
