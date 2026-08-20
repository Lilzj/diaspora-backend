namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where the event happens.
/// <para>
/// Present because it changes what the venue means. An online event with a required address field
/// is a form nobody can fill in honestly, and a hybrid event with only one is a form that loses
/// half the answer.
/// </para>
/// </summary>
public enum EventAttendanceMode
{
    /// <summary>Somewhere you go.</summary>
    InPerson = 0,

    /// <summary>Somewhere you connect to. No venue, and no check-in at a door.</summary>
    Online = 1,

    /// <summary>Both, with a real venue and a link.</summary>
    Hybrid = 2
}
