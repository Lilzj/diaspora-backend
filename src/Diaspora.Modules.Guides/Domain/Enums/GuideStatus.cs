namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Where a guide is in its life. Three states, deliberately: a guide is being written, is
/// published, or has been retired — moderation visibility is a separate axis
/// (<see cref="GuideModerationState"/>) because a hidden guide is still a published guide.
/// </summary>
public enum GuideStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
