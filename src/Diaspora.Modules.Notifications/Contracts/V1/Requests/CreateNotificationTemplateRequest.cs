namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>Creates a draft template. Staff only.</summary>
public sealed record CreateNotificationTemplateRequest
{
    public required string Key { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary><c>Critical</c>, <c>High</c>, <c>Normal</c>, <c>Low</c> or <c>Silent</c>.</summary>
    public required string Priority { get; init; }

    /// <summary>Must appear in <see cref="Localizations"/> before the template can be published.</summary>
    public string DefaultLanguage { get; init; } = "en";

    public string? Description { get; init; }

    /// <summary>An un-substituted client route for the card itself.</summary>
    public string? RouteTemplate { get; init; }

    /// <summary>How long messages from this template live. Null inherits the deployment default.</summary>
    public int? RetentionDays { get; init; }

    public required IReadOnlyList<TemplateLocalizationRequest> Localizations { get; init; }

    public IReadOnlyList<TemplateVariableRequest> Variables { get; init; } = [];

    public IReadOnlyList<TemplateVariantRequest> Variants { get; init; } = [];

    public IReadOnlyList<TemplateActionRequest> Actions { get; init; } = [];
}
