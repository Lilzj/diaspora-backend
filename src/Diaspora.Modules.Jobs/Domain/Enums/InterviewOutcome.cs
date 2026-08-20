namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What an interview concluded.
/// <para>
/// <see cref="Inconclusive"/> exists because it is the honest answer often enough to matter,
/// and because forcing a hiring manager to record a pass or a fail they do not believe makes
/// the whole record worthless. <see cref="NoShow"/> is separate from <see cref="Failed"/> for
/// the same reason: they are different facts, and a candidate whose visa appointment overran
/// has not failed an interview.
/// </para>
/// </summary>
public enum InterviewOutcome
{
    /// <summary>Not yet recorded. Every interview starts here.</summary>
    Pending = 0,

    Passed = 1,

    Failed = 2,

    /// <summary>The candidate did not attend.</summary>
    NoShow = 3,

    /// <summary>Called off before it happened.</summary>
    Cancelled = 4,

    /// <summary>Held, but decided nothing.</summary>
    Inconclusive = 5
}
