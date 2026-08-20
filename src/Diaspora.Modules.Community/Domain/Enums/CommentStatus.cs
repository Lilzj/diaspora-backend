namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Where a comment stands.
/// <para>
/// Names verbatim from the client, plus <see cref="Deleted"/>, which its soft-delete path
/// implies but does not declare.
/// </para>
/// <para>
/// A removed or deleted comment with replies is <b>tombstoned rather than erased</b>: the row
/// survives, the body is replaced at projection time, and the thread keeps its shape. A
/// conversation whose third message vanished and took its four replies with it is worse for
/// every remaining participant than one that says a message was removed.
/// </para>
/// </summary>
public enum CommentStatus
{
    Active = 0,

    /// <summary>At least one open report. Still visible; flagged in the moderation queue.</summary>
    Reported = 1,

    /// <summary>Hidden by moderation. Author and staff still see it.</summary>
    Hidden = 2,

    /// <summary>Removed by moderation. Tombstoned in the thread.</summary>
    Removed = 3,

    /// <summary>Deleted by its author. Tombstoned in the thread.</summary>
    Deleted = 4
}
