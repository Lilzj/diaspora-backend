using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Templates;

/// <summary>
/// Applies the parts of a template request to a draft aggregate.
/// <para>
/// Shared by create and update because both take the same four collections and both must apply them
/// the same way — two implementations would drift on exactly the detail that matters, which is that
/// a collection the request omits is left alone rather than emptied.
/// </para>
/// </summary>
public sealed class TemplateAssembler
{
    private readonly IGuidGenerator _guids;

    public TemplateAssembler(IGuidGenerator guids) => _guids = guids;

    public Result Apply(
        NotificationTemplate template,
        IReadOnlyList<TemplateLocalizationRequest>? localizations,
        IReadOnlyList<TemplateVariableRequest>? variables,
        IReadOnlyList<TemplateVariantRequest>? variants,
        IReadOnlyList<TemplateActionRequest>? actions)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (variables is not null)
        {
            var applied = template.SetVariables(variables.Select(variable =>
                TemplateVariable.Create(
                    _guids.NewSequentialGuid(),
                    variable.Name,
                    variable.IsRequired,
                    variable.Format,
                    variable.Sample)));

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        foreach (var localization in localizations ?? [])
        {
            var applied = template.SetLocalization(TemplateLocalization.Create(
                _guids.NewSequentialGuid(),
                localization.Language,
                localization.Title,
                localization.Body,
                localization.ShortBody,
                localization.PluralBody,
                NotificationMapper.ParseEnum<ContentFormat>(localization.Format) ?? ContentFormat.Plain));

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        foreach (var variant in variants ?? [])
        {
            var channel = NotificationMapper.ParseEnum<NotificationChannelKind>(variant.Channel);

            if (channel is not { } kind)
            {
                continue;
            }

            var applied = template.SetVariant(TemplateChannelVariant.Create(
                _guids.NewSequentialGuid(),
                kind,
                variant.Language,
                variant.Title,
                variant.Body,
                NotificationMapper.ParseEnum<ContentFormat>(variant.Format)));

            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (actions is null)
        {
            return Result.Success();
        }

        return template.SetActions(actions.Select(action =>
            TemplateActionDefinition.Create(
                _guids.NewSequentialGuid(),
                action.Key,
                action.Label,
                action.RouteTemplate,
                NotificationMapper.ParseEnum<NotificationActionStyle>(action.Style)
                    ?? NotificationActionStyle.Primary,
                action.Position)));
    }
}
