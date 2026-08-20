namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>One action, flattened for a transport that has no concept of a value object.</summary>
public sealed record ChannelAction
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Route { get; init; }

    /// <summary><c>Primary</c>, <c>Secondary</c> or <c>Destructive</c>.</summary>
    public required string Style { get; init; }
}
