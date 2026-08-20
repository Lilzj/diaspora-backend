namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>A per-channel override.</summary>
public sealed record TemplateVariantRequest
{
    public required string Channel { get; init; }

    public required string Language { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    public string? Format { get; init; }
}
