namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What a report is about.
/// <para>
/// One report aggregate covering both subjects rather than two, because the moderation queue is one
/// queue: a moderator working through complaints does not want two screens, and the counts that
/// drive auto-flagging are the same arithmetic on both.
/// </para>
/// </summary>
public enum ReportSubjectType
{
    Event = 0,

    Organization = 1
}
