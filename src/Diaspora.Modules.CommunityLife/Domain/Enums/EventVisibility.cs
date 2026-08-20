namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Who can reach an event, independent of its lifecycle state.
/// <para>
/// <see cref="MembersOnly"/> means the organizing organization's members, not the platform's — a
/// distinction that matters because it is the setting a small association uses for the meeting it
/// does not want strangers at, and reading it as "any signed-in member" would put strangers there.
/// </para>
/// </summary>
public enum EventVisibility
{
    /// <summary>Organizers only. Where every event starts.</summary>
    Private = 0,

    /// <summary>Anyone, signed in or not.</summary>
    Public = 1,

    /// <summary>Members of the organizing organization.</summary>
    MembersOnly = 2,

    /// <summary>Reachable by direct link but absent from search and browse.</summary>
    Unlisted = 3
}
