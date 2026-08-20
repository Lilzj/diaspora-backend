namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>A per-channel override. Anything null inherits from the localization.</summary>
public sealed record TemplateVariantResponse
{
    public required string Channel { get; init; }

    public required string Language { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    public string? Format { get; init; }
}
