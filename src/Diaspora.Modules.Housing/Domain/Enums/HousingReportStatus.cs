namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>Where a report has got to in the moderation queue.</summary>
public enum HousingReportStatus
{
    Open = 0,
    UnderReview = 1,
    Upheld = 2,
    Dismissed = 3
}
