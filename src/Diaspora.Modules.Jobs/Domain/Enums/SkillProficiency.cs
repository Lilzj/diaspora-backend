namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How strong a candidate's command of a skill is, or how strong a posting needs it to be.
/// <para>
/// Ordered, so a posting requiring <see cref="Advanced"/> can be compared against a candidate
/// claiming <see cref="Expert"/>. The comparison is a filter input and nothing more — B9
/// implements no matching model, and AI candidate matching is explicitly out of scope.
/// </para>
/// </summary>
public enum SkillProficiency
{
    /// <summary>Not stated.</summary>
    Unspecified = 0,

    Beginner = 1,

    Intermediate = 2,

    Advanced = 3,

    Expert = 4
}
