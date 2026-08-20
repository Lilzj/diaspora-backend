namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// Why a message left the inbox.
/// <para>
/// Three different facts that a single boolean would flatten. Filing something is not refusing it,
/// and neither is the retention sweep aging it out — and the difference decides both what the
/// analytics say about a template and what the member is told when they go looking for it.
/// </para>
/// </summary>
public enum ArchiveReason
{
    /// <summary>The member filed it.</summary>
    Archived = 0,

    /// <summary>The member refused it — swiped it away, tapped "not interested".</summary>
    Dismissed = 1,

    /// <summary>The retention sweep aged it out. Nobody chose this.</summary>
    Expired = 2
}
