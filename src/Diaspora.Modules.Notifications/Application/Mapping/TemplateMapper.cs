using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Templates;

namespace Diaspora.Modules.Notifications.Application.Mapping;

/// <summary>Turns templates into wire responses for the staff authoring surface.</summary>
public static class TemplateMapper
{
    public static NotificationTemplateResponse ToResponse(NotificationTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new NotificationTemplateResponse
        {
            Id = template.Id.Value,
            Key = template.Key,
            Version = template.Version,
            CategoryKey = template.CategoryKey,
            Priority = template.Priority.ToString(),
            Status = template.Status.ToString(),
            DefaultLanguage = template.DefaultLanguage,
            Description = template.Description,
            RouteTemplate = template.RouteTemplate,
            RetentionDays = template.RetentionPeriod is { } retention
                ? (int)retention.TotalDays
                : null,
            PublishedAt = template.PublishedAt,
            Localizations = template.Localizations
                .Select(localization => new TemplateLocalizationResponse
                {
                    Language = localization.Language,
                    Title = localization.Title,
                    Body = localization.Body,
                    ShortBody = localization.ShortBody,
                    PluralBody = localization.PluralBody,
                    Format = localization.Format.ToString()
                })
                .ToList(),
            Variants = template.Variants
                .Select(variant => new TemplateVariantResponse
                {
                    Channel = variant.Channel.ToString(),
                    Language = variant.Language,
                    Title = variant.Title,
                    Body = variant.Body,
                    Format = variant.Format?.ToString()
                })
                .ToList(),
            Variables = template.Variables
                .Select(variable => new TemplateVariableResponse
                {
                    Name = variable.Name,
                    IsRequired = variable.IsRequired,
                    Format = variable.Format,
                    Sample = variable.Sample
                })
                .ToList(),
            Actions = template.Actions
                .Select(action => new TemplateActionResponse
                {
                    Key = action.Key,
                    Label = action.Label,
                    RouteTemplate = action.RouteTemplate,
                    Style = action.Style.ToString(),
                    Position = action.Position
                })
                .ToList()
        };
    }
}
