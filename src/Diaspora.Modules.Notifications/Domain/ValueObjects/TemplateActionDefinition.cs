using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// An action a template offers, before any particular message exists.
/// <para>
/// The route here is a <em>template</em> — <c>/jobs/{jobId}</c> — and the variables in it are the
/// same ones the body uses. Substitution happens once, at render time, and the result is stored on
/// the notification as a finished <see cref="DeepLink"/>. Keeping the template on the message and
/// substituting on read would mean an edit to this reference data could silently repoint every
/// message ever sent from it.
/// </para>
/// </summary>
public sealed class TemplateActionDefinition : ValueObject
{
    private TemplateActionDefinition(
        Guid actionId,
        string key,
        string label,
        string? routeTemplate,
        NotificationActionStyle style,
        int position)
    {
        ActionId = actionId;
        Key = key;
        Label = label;
        RouteTemplate = routeTemplate;
        Style = style;
        Position = position;
    }

    private TemplateActionDefinition() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid ActionId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// The default label, in the template's default language.
    /// <para>
    /// Translated labels live on the localization as <c>action.{key}</c> variables rather than being
    /// duplicated per language here — one localization row per language beats one action row per
    /// language per action, and the alternative was measured at four times the rows for no extra
    /// expressiveness.
    /// </para>
    /// </summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>An un-substituted client route. Null for an action that only closes the card.</summary>
    public string? RouteTemplate { get; private set; }

    public NotificationActionStyle Style { get; private set; }

    /// <summary>Lower renders first. Ties break on key so the order is never non-deterministic.</summary>
    public int Position { get; private set; }

    public static TemplateActionDefinition Create(
        Guid actionId,
        string key,
        string label,
        string? routeTemplate = null,
        NotificationActionStyle style = NotificationActionStyle.Primary,
        int position = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new TemplateActionDefinition(
            actionId,
            Clip(key, NotificationLimits.MaxKeyLength)!,
            Clip(label, NotificationLimits.MaxActionLabelLength)!,
            Clip(routeTemplate, NotificationLimits.MaxDeepLinkLength),
            style,
            Math.Max(0, position));
    }

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ActionId;
        yield return Key;
        yield return Label;
        yield return RouteTemplate;
        yield return Style;
        yield return Position;
    }
}
