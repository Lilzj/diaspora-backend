namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One template's words in one language.</summary>
public sealed record TemplateLocalizationResponse
{
    public required string Language { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public string? ShortBody { get; init; }

    /// <summary>The form used when a count is not one.</summary>
    public string? PluralBody { get; init; }

    /// <summary><c>Plain</c> or <c>Markdown</c>. <c>Html</c> is refused by validation.</summary>
    public required string Format { get; init; }
}
