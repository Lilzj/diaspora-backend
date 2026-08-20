namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Where an application sits in the hiring workflow.
/// <para>
/// The brief's nine states exactly. Three of them are terminal — <see cref="Accepted"/>,
/// <see cref="Rejected"/> and <see cref="Withdrawn"/> — and the rest are forward-only;
/// <c>ApplicationStatusTransitions</c> is the single place the legal moves are declared, and
/// the aggregate refuses anything else.
/// </para>
/// <para>
/// <b>Withdrawn is reachable from every non-terminal state, and that is a deliberate asymmetry
/// in the candidate's favour.</b> An employer must walk an application forward one step at a
/// time; a candidate may leave at any point, including the day before an offer. Anything else
/// would make the platform a trap.
/// </para>
/// <para>
/// Every transition is recorded on the application's own append-only history, so "when was I
/// shortlisted" is answerable months later — which is the difference between a status column
/// and an auditable hiring record.
/// </para>
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Received. The candidate has done everything they can do unprompted.</summary>
    Submitted = 1,

    /// <summary>An employer has opened it.</summary>
    UnderReview = 2,

    /// <summary>Through the first cut.</summary>
    Shortlisted = 3,

    InterviewScheduled = 4,

    InterviewCompleted = 5,

    OfferExtended = 6,

    /// <summary>The candidate accepted. Terminal, and the state that can fill a vacancy.</summary>
    Accepted = 7,

    /// <summary>The employer declined. Terminal.</summary>
    Rejected = 8,

    /// <summary>The candidate withdrew. Terminal, and reachable from anywhere before the end.</summary>
    Withdrawn = 9
}
