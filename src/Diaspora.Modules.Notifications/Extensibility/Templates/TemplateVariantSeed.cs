using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>A per-channel override, as a contributor states it.</summary>
public sealed record TemplateVariantSeed
{
    public required NotificationChannelKind Channel { get; init; }

    public required string Language { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    public ContentFormat? Format { get; init; }
}
