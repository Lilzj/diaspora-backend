using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Whether a member wants one whole transport at all.
/// <para>
/// The coarse switch, above every category. A member who turns push off has turned push off for
/// everything except Critical — and the preference screen says so rather than implying a security
/// alert can be silenced.
/// </para>
/// </summary>
public sealed class ChannelPreference : ValueObject
{
    private ChannelPreference(NotificationChannelKind channel, bool isEnabled)
    {
        Channel = channel;
        IsEnabled = isEnabled;
    }

    private ChannelPreference() { }

    public NotificationChannelKind Channel { get; private set; }

    public bool IsEnabled { get; private set; }

    public static ChannelPreference Create(NotificationChannelKind channel, bool isEnabled) =>
        new(channel, isEnabled);

    /// <summary>
    /// The defaults a new member starts with.
    /// <para>
    /// In-app on and not switchable, push on, email off. Email off is the deliberate one: a member
    /// who has just signed up did not ask for mail, and an unsolicited first email is how a platform
    /// teaches its members to mark it as spam.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ChannelPreference> Defaults() =>
    [
        new(NotificationChannelKind.InApp, true),
        new(NotificationChannelKind.Push, true),
        new(NotificationChannelKind.Email, false)
    ];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Channel;
        yield return IsEnabled;
    }
}
