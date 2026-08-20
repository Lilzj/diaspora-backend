namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>Somebody billed to speak at an event.</summary>
public sealed record EventSpeakerResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Title { get; init; }

    public string? Organization { get; init; }

    public string? Biography { get; init; }

    public Guid? PhotoMediaId { get; init; }

    public Guid? MemberUserId { get; init; }

    public required int Order { get; init; }
}
