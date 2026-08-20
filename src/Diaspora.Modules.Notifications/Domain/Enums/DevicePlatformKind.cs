namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// What kind of client an endpoint belongs to.
/// <para>
/// Mirrors the vocabulary Identity's device record already uses, so the two can be correlated by a
/// human reading both without a translation table. It is repeated rather than shared because
/// importing Identity's enum would be a project reference taken for four constants.
/// </para>
/// </summary>
public enum DevicePlatformKind
{
    Unknown = 0,

    Android = 1,

    IOS = 2,

    Web = 3,

    Tablet = 4
}
