namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>
/// Edits a draft template.
/// <para>
/// A published template is never edited. Sending this against one creates the next draft version
/// from it and applies the change there, which is what makes a wording change reversible without a
/// deployment.
/// </para>
/// </summary>
public sealed record UpdateNotificationTemplateRequest
{
    public string? Description { get; init; }

    public string? RouteTemplate { get; init; }

    public string? Priority { get; init; }

    public int? RetentionDays { get; init; }

    public IReadOnlyList<TemplateLocalizationRequest>? Localizations { get; init; }

    public IReadOnlyList<TemplateVariableRequest>? Variables { get; init; }

    public IReadOnlyList<TemplateVariantRequest>? Variants { get; init; }

    public IReadOnlyList<TemplateActionRequest>? Actions { get; init; }
}
