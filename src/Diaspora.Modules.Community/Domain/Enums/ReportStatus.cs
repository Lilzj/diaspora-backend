namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Where a report is in the moderation workflow. Names verbatim from the client.
/// </summary>
public enum ReportStatus
{
    /// <summary>Filed, nobody has looked.</summary>
    Open = 0,

    /// <summary>A moderator has read it and not yet decided.</summary>
    Reviewed = 1,

    /// <summary>Decided, and the decision changed the content.</summary>
    Resolved = 2,

    /// <summary>Decided, and the content was left alone.</summary>
    Dismissed = 3
}
