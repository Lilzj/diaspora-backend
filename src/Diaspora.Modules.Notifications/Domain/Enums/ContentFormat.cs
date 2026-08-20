namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// How a rendered body should be interpreted.
/// <para>
/// <b>Plain and Markdown ship; Html is declared and refused.</b> Accepting HTML from a template
/// author means accepting a script tag from a template author, and the sanitiser that would make it
/// safe is a dependency this module does not need in order to send "your interview is at 14:00".
/// The value exists so an email channel can request it later without a contract change; the
/// template validator rejects it today with a reason that says so.
/// </para>
/// </summary>
public enum ContentFormat
{
    Plain = 0,

    Markdown = 1,

    /// <summary>Reserved. Rejected by template validation until a sanitiser is part of the platform.</summary>
    Html = 2
}
