namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One tappable action on a notification card.</summary>
public sealed record NotificationActionResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? TargetRoute { get; init; }

    /// <summary><c>Primary</c>, <c>Secondary</c> or <c>Destructive</c>. A style, never a colour.</summary>
    public required string Style { get; init; }
}
