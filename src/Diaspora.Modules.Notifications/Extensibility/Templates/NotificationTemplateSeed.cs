using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>
/// A template as a contributor declares it, before it is an aggregate.
/// <para>
/// A flat record rather than the aggregate itself, so a contributor cannot be handed a half-built
/// entity and cannot reach the repository. It declares; the seeder decides what that means for rows
/// that already exist.
/// </para>
/// </summary>
public sealed record NotificationTemplateSeed
{
    public required string Key { get; init; }

    public required string CategoryKey { get; init; }

    public required NotificationPriority Priority { get; init; }

    /// <summary>Must appear in <see cref="Localizations"/>, or the seed is rejected.</summary>
    public string DefaultLanguage { get; init; } = "en";

    public string? Description { get; init; }

    /// <summary>An un-substituted client route for the card itself.</summary>
    public string? RouteTemplate { get; init; }

    /// <summary>How long messages from this template stay in the inbox. Null means forever.</summary>
    public TimeSpan? RetentionPeriod { get; init; }

    public required IReadOnlyList<TemplateLocalizationSeed> Localizations { get; init; }

    public IReadOnlyList<TemplateVariableSeed> Variables { get; init; } = [];

    public IReadOnlyList<TemplateVariantSeed> Variants { get; init; } = [];

    public IReadOnlyList<TemplateActionSeed> Actions { get; init; } = [];

    /// <summary>
    /// Whether the seeder should publish it immediately.
    /// <para>
    /// True for everything shipped with a module: a template that arrives as a draft is a
    /// notification the platform silently fails to send until somebody notices and presses a button.
    /// </para>
    /// </summary>
    public bool PublishOnSeed { get; init; } = true;
}
