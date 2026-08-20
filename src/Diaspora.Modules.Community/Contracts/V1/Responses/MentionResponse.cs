namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>Somewhere the caller was mentioned.</summary>
public sealed record MentionResponse
{
    public required Guid MentionId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid MentionedBy { get; init; }

    public required string TargetType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
