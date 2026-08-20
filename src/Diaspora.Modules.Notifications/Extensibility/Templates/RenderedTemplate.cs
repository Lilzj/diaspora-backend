using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>
/// The finished words, ready to be frozen onto a notification or handed to a channel.
/// </summary>
public sealed record RenderedTemplate
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>The push-length form, when the template or the length required one.</summary>
    public string? ShortBody { get; init; }

    /// <summary>Which language it actually came out in, after the fallback chain walked.</summary>
    public required string Language { get; init; }

    public required ContentFormat Format { get; init; }

    /// <summary>The card's own route, substituted. Null when the template offers none.</summary>
    public string? Route { get; init; }

    public IReadOnlyList<RenderedAction> Actions { get; init; } = [];
}
