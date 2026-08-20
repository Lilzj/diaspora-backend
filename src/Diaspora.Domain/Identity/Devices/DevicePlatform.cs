namespace Diaspora.Domain.Identity.Devices;

/// <summary>
/// The client platform a session or device belongs to.
/// Tablet is carried from the outset so the value never has to be back-filled.
/// </summary>
public enum DevicePlatform
{
    Unknown = 0,
    Android = 1,
    IOS = 2,
    Web = 3,
    Tablet = 4
}
