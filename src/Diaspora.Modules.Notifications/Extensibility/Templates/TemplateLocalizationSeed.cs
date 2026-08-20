using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>One language's words, as a contributor declares them.</summary>
public sealed record TemplateLocalizationSeed
{
    public required string Language { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The push-length form. Omitted where the body already fits.</summary>
    public string? ShortBody { get; init; }

    /// <summary>The form used when a count is not one.</summary>
    public string? PluralBody { get; init; }

    public ContentFormat Format { get; init; } = ContentFormat.Plain;
}
