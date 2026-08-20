namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One row of the category catalogue, for a client rendering a settings screen.</summary>
public sealed record NotificationCategoryResponse
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string DefaultPriority { get; init; }

    public required bool IsMandatory { get; init; }

    public required bool DigestByDefault { get; init; }

    public required string OwnerModule { get; init; }

    public required int Position { get; init; }
}
