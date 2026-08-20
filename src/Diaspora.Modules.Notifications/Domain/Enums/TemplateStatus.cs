namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// Whether a template may be used to speak in the platform's name.
/// <para>
/// <b>Draft is not "unfinished", it is "cannot be sent".</b> The dispatcher refuses any template
/// that is not Published, which is what stops a half-translated security warning from reaching two
/// hundred thousand people because somebody saved it.
/// </para>
/// </summary>
public enum TemplateStatus
{
    Draft = 0,

    Published = 1,

    /// <summary>
    /// Retired. Existing notifications that reference it still render from the version they
    /// captured; nothing new may use it. Deactivating is reversible; deleting is not offered.
    /// </summary>
    Deactivated = 2
}
