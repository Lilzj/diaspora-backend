using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Turns a contributor's declaration into a published template aggregate.
/// <para>
/// <b>It publishes on the way, and that is the important part.</b> A template that arrived as a draft
/// would be a notification the platform silently fails to send until somebody notices and presses a
/// button — which is exactly the failure mode nobody discovers until a member complains that they
/// were never told about their interview.
/// </para>
/// <para>
/// A seed that fails validation is skipped with a loud log rather than throwing. One malformed
/// contribution must not stop the other thirty from seeding, and it must not take the host down.
/// </para>
/// </summary>
public sealed class NotificationTemplateSeedAssembler
{
    private readonly ILogger<NotificationTemplateSeedAssembler> _logger;

    public NotificationTemplateSeedAssembler(ILogger<NotificationTemplateSeedAssembler> logger) =>
        _logger = logger;

    public NotificationTemplate? Build(
        NotificationTemplateSeed seed, IGuidGenerator guids, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(guids);

        var draft = NotificationTemplate.Draft(
            NotificationTemplateId.From(guids.NewSequentialGuid()),
            seed.Key,
            seed.CategoryKey,
            seed.Priority,
            now,
            version: 1,
            seed.DefaultLanguage,
            seed.Description,
            seed.RouteTemplate,
            seed.RetentionPeriod);

        if (draft.IsFailure)
        {
            _logger.LogError(
                "Seeded template '{TemplateKey}' is not valid: {Error}. It was skipped.",
                seed.Key,
                draft.Error.Code);

            return null;
        }

        var template = draft.Value;

        template.SetVariables(seed.Variables.Select(variable =>
            TemplateVariable.Create(
                guids.NewSequentialGuid(),
                variable.Name,
                variable.IsRequired,
                variable.Format,
                variable.Sample)));

        foreach (var localization in seed.Localizations)
        {
            template.SetLocalization(TemplateLocalization.Create(
                guids.NewSequentialGuid(),
                localization.Language,
                localization.Title,
                localization.Body,
                localization.ShortBody,
                localization.PluralBody,
                localization.Format));
        }

        foreach (var variant in seed.Variants)
        {
            template.SetVariant(TemplateChannelVariant.Create(
                guids.NewSequentialGuid(),
                variant.Channel,
                variant.Language,
                variant.Title,
                variant.Body,
                variant.Format));
        }

        template.SetActions(seed.Actions.Select(action =>
            TemplateActionDefinition.Create(
                guids.NewSequentialGuid(),
                action.Key,
                action.Label,
                action.RouteTemplate,
                action.Style,
                action.Position)));

        if (!seed.PublishOnSeed)
        {
            return template;
        }

        var published = template.Publish(now);

        if (published.IsSuccess)
        {
            return template;
        }

        // Loud: a shipped template that will not publish is a message the platform intends to send
        // and silently cannot, and the reason is always a declaration mistake somebody can fix.
        _logger.LogError(
            "Seeded template '{TemplateKey}' could not be published: {Error}. It was skipped.",
            seed.Key,
            published.Error.Code);

        return null;
    }
}
