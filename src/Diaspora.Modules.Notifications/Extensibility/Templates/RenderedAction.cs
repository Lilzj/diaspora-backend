using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>One action with its route substituted.</summary>
public sealed record RenderedAction
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Route { get; init; }

    public required NotificationActionStyle Style { get; init; }
}
