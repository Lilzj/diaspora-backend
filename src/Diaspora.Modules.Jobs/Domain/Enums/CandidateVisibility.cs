namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Who may see a candidate's professional profile.
/// <para>
/// <b><see cref="Private"/> is the default, and that choice is not neutral.</b> Someone
/// job-hunting while employed can be fired for being found, and a diaspora member's profile
/// carries their nationality, their visa status and their location. A product that defaults to
/// discoverable is making that decision on their behalf and getting it wrong for the people
/// with the most to lose.
/// </para>
/// <para>
/// Applying is unaffected by any of this: a candidate who applies has chosen to show that
/// employer their profile, and <see cref="Private"/> never hides it from an employer they
/// approached.
/// </para>
/// </summary>
public enum CandidateVisibility
{
    /// <summary>Visible only to the candidate, and to employers they have applied to.</summary>
    Private = 0,

    /// <summary>Visible to verified employers searching the platform.</summary>
    VerifiedEmployers = 1,

    /// <summary>Visible to any signed-in member.</summary>
    Members = 2,

    /// <summary>Publicly visible.</summary>
    Public = 3
}
