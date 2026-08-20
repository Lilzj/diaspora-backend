using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// One tappable thing on a notification card.
/// <para>
/// An action is a label and a link, never a command. A notification that could accept an offer
/// would be a notification that could accept an offer from a stolen phone on a lock screen, and it
/// would put Jobs' business rules inside the Communication Platform. Every action navigates; the
/// module that owns the destination decides what happens there.
/// </para>
/// <para>
/// <see cref="Key"/> is what the click event reports, so "which button did people press" is
/// answerable without parsing labels that change with every translation.
/// </para>
/// </summary>
public sealed class NotificationAction : ValueObject
{
    private NotificationAction(
        Guid actionId, string key, string label, NotificationActionStyle style, DeepLink? link)
    {
        ActionId = actionId;
        Key = key;
        Label = label;
        Style = style;
        Link = link;
    }

    private NotificationAction() { }

    /// <summary>
    /// The owned collection's key, supplied by the caller.
    /// <para>
    /// Caller-supplied rather than store-generated, and the persistence configuration must declare
    /// <c>ValueGeneratedNever</c> for it — otherwise EF treats a populated key as an existing row
    /// and emits an UPDATE that matches nothing, which surfaces as a bogus concurrency conflict.
    /// </para>
    /// </summary>
    public Guid ActionId { get; private set; }

    /// <summary>Stable machine name — <c>view</c>, <c>reply</c>, <c>dismiss</c>.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The rendered, translated words on the control.</summary>
    public string Label { get; private set; } = string.Empty;

    public NotificationActionStyle Style { get; private set; }

    /// <summary>Where it goes. Null for an action whose only effect is to close the card.</summary>
    public DeepLink? Link { get; private set; }

    public static NotificationAction Create(
        Guid actionId,
        string key,
        string label,
        NotificationActionStyle style = NotificationActionStyle.Primary,
        DeepLink? link = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return new NotificationAction(
            actionId,
            Clip(key, NotificationLimits.MaxKeyLength),
            Clip(label, NotificationLimits.MaxActionLabelLength),
            style,
            link);
    }

    private static string Clip(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ActionId;
        yield return Key;
        yield return Label;
        yield return Style;
        yield return Link;
    }
}
