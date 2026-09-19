namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>Where a report about a job advert sits.</summary>
public enum JobReportStatus
{
    /// <summary>Filed, and nobody has looked yet.</summary>
    Open = 0,

    /// <summary>A moderator has it.</summary>
    UnderReview = 1,

    /// <summary>The complaint was right, and something was done about the posting.</summary>
    Upheld = 2,

    /// <summary>Looked at, and the advert was fine.</summary>
    Dismissed = 3
}
