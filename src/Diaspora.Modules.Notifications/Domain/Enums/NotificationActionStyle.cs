namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// How a client should render one of a message's actions.
/// <para>
/// A style rather than a colour, so the client owns its own design language. This module states
/// intent — "this is the destructive one" — and never a hex value.
/// </para>
/// </summary>
public enum NotificationActionStyle
{
    Primary = 0,

    Secondary = 1,

    Destructive = 2
}
