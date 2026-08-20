namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How much a spam signal is worth.
/// <para>
/// Severity rather than a score, because the module composes signals from providers it did not
/// write and cannot make their numbers commensurable. Three named levels can be combined honestly;
/// two providers returning 0.7 cannot.
/// </para>
/// </summary>
public enum EventSpamSignalSeverity
{
    /// <summary>Worth a moderator's note and nothing else.</summary>
    Informational = 0,

    /// <summary>Enough to flag, in combination.</summary>
    Warning = 1,

    /// <summary>Enough to hold publication on its own.</summary>
    Severe = 2
}
