namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>One language's words.</summary>
public sealed record TemplateLocalizationRequest
{
    public required string Language { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public string? ShortBody { get; init; }

    public string? PluralBody { get; init; }

    /// <summary><c>Plain</c> or <c>Markdown</c>. <c>Html</c> is refused by validation.</summary>
    public string Format { get; init; } = "Plain";
}
