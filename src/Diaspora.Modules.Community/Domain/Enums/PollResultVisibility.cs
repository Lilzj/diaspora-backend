namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// When a member may see a poll result.
/// <para>
/// Not decoration. Showing a running tally before someone votes measurably moves their vote,
/// which for a community poll about, say, whether a neighbourhood feels safe is the difference
/// between a signal and an echo. <see cref="AfterVoting"/> is the default for that reason.
/// </para>
/// </summary>
public enum PollResultVisibility
{
    /// <summary>Only once the member has voted. The default.</summary>
    AfterVoting = 0,

    /// <summary>Always, to anyone who can see the poll.</summary>
    Always = 1,

    /// <summary>Only once the poll has closed.</summary>
    AfterClosing = 2
}
