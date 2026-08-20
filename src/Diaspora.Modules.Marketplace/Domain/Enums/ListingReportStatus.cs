namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>Where a report sits in the moderation queue.</summary>
public enum ListingReportStatus
{
    /// <summary>Filed, untouched.</summary>
    Open = 0,

    /// <summary>Picked up by a moderator.</summary>
    Reviewing = 1,

    /// <summary>Upheld, and something was done to the listing.</summary>
    Actioned = 2,

    /// <summary>Looked at and found to be nothing.</summary>
    Dismissed = 3
}
