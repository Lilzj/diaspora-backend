namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>What a moderator has done about a report.</summary>
public enum CommunityLifeReportStatus
{
    /// <summary>Filed, and nobody has looked yet.</summary>
    Open = 0,

    /// <summary>A moderator has it.</summary>
    UnderReview = 1,

    /// <summary>Acted on.</summary>
    Upheld = 2,

    /// <summary>Looked at, and nothing was wrong.</summary>
    Dismissed = 3,

    /// <summary>The same complaint as one already open.</summary>
    Duplicate = 4
}
