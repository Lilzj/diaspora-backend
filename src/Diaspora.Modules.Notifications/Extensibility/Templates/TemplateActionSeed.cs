using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>An action a template offers, as a contributor states it.</summary>
public sealed record TemplateActionSeed
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? RouteTemplate { get; init; }

    public NotificationActionStyle Style { get; init; } = NotificationActionStyle.Primary;

    public int Position { get; init; }
}
