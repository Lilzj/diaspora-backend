namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One line in a digest. A snapshot, taken when the digest was built.</summary>
public sealed record DigestEntryResponse
{
    public required Guid NotificationId { get; init; }

    public required string CategoryKey { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? TargetRoute { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
