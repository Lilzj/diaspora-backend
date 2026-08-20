namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One authored template, for the staff authoring surface.</summary>
public sealed record NotificationTemplateResponse
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required int Version { get; init; }

    public required string CategoryKey { get; init; }

    public required string Priority { get; init; }

    /// <summary><c>Draft</c>, <c>Published</c> or <c>Deactivated</c>.</summary>
    public required string Status { get; init; }

    public required string DefaultLanguage { get; init; }

    public string? Description { get; init; }

    public string? RouteTemplate { get; init; }

    /// <summary>How long messages from this template live. Null means forever.</summary>
    public int? RetentionDays { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required IReadOnlyList<TemplateLocalizationResponse> Localizations { get; init; }

    public IReadOnlyList<TemplateVariantResponse> Variants { get; init; } = [];

    public IReadOnlyList<TemplateVariableResponse> Variables { get; init; } = [];

    public IReadOnlyList<TemplateActionResponse> Actions { get; init; } = [];
}
