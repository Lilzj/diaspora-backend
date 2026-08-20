namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// The moderation axis, independent of <see cref="GuideStatus"/>. Flagged keeps the guide
/// visible while staff look; Hidden removes it from every surface without touching the
/// author's draft state, so lifting the decision restores exactly what was there.
/// </summary>
public enum GuideModerationState
{
    Clear = 0,
    Flagged = 1,
    Hidden = 2
}
