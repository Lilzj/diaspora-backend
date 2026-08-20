using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Policies;

/// <summary>
/// The legal moves through an application's workflow, declared once.
/// <para>
/// <b>The asymmetry is the design.</b> An employer walks an application forward one step at a
/// time and may reject at any point; a candidate may withdraw at any point, including the day
/// before an offer, and may do so from every non-terminal state. Anything else would make the
/// platform a trap for the person with the least power in the transaction.
/// </para>
/// <para>
/// <b>Backward moves are deliberately absent, with one exception.</b> An application that was
/// shortlisted and then interviewed does not go back to <c>UnderReview</c>: the history is the
/// record, and rewinding a status would make "how long was I at interview" unanswerable. The
/// exception is <see cref="ApplicationStatus.InterviewScheduled"/> back to
/// <see cref="ApplicationStatus.Shortlisted"/>, because a cancelled interview genuinely returns
/// the candidate to the shortlist and there is nowhere else honest to put them.
/// </para>
/// </summary>
public static class ApplicationStatusTransitions
{
    /// <summary>
    /// The states an application can no longer move from. Every guard in the aggregate checks
    /// this before anything else.
    /// </summary>
    private static readonly ApplicationStatus[] Terminal =
    [
        ApplicationStatus.Accepted,
        ApplicationStatus.Rejected,
        ApplicationStatus.Withdrawn
    ];

    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Submitted] =
        [
            ApplicationStatus.UnderReview,
            ApplicationStatus.Shortlisted,
            ApplicationStatus.Rejected
        ],

        [ApplicationStatus.UnderReview] =
        [
            ApplicationStatus.Shortlisted,
            ApplicationStatus.InterviewScheduled,
            ApplicationStatus.Rejected
        ],

        [ApplicationStatus.Shortlisted] =
        [
            ApplicationStatus.InterviewScheduled,

            // Some employers offer straight off a shortlist. Uncommon, entirely legitimate.
            ApplicationStatus.OfferExtended,
            ApplicationStatus.Rejected
        ],

        [ApplicationStatus.InterviewScheduled] =
        [
            ApplicationStatus.InterviewCompleted,

            // A cancelled interview returns the candidate to the shortlist.
            ApplicationStatus.Shortlisted,
            ApplicationStatus.Rejected
        ],

        [ApplicationStatus.InterviewCompleted] =
        [
            // Another round.
            ApplicationStatus.InterviewScheduled,
            ApplicationStatus.OfferExtended,
            ApplicationStatus.Rejected
        ],

        [ApplicationStatus.OfferExtended] =
        [
            ApplicationStatus.Accepted,

            // The candidate turning an offer down is recorded as a rejection of the
            // application, with the offer aggregate holding who declined and why.
            ApplicationStatus.Rejected
        ],

        // Terminal.
        [ApplicationStatus.Accepted] = [],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Withdrawn] = []
    };

    /// <summary>Whether an application may move from one status to another.</summary>
    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (from == to || IsTerminal(from))
        {
            return false;
        }

        // The candidate's exit, available from everywhere that is not already an ending.
        if (to == ApplicationStatus.Withdrawn)
        {
            return true;
        }

        return Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;
    }

    /// <summary>Whether an application has reached an ending.</summary>
    public static bool IsTerminal(ApplicationStatus status) =>
        Array.IndexOf(Terminal, status) >= 0;

    /// <summary>Everything an application in this status may become, the candidate's exit included.</summary>
    public static IReadOnlyList<ApplicationStatus> From(ApplicationStatus status)
    {
        if (IsTerminal(status))
        {
            return [];
        }

        var targets = Allowed.TryGetValue(status, out var declared) ? declared : [];

        return [.. targets, ApplicationStatus.Withdrawn];
    }

    /// <summary>
    /// Whether this status means the employer is actively considering the candidate. Drives
    /// the "active applications" count on both dashboards.
    /// </summary>
    public static bool IsActive(ApplicationStatus status) => !IsTerminal(status);

    /// <summary>The hiring stage kind an application in this status naturally sits at.</summary>
    public static HiringStageKind ToStageKind(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Submitted => HiringStageKind.Applied,
        ApplicationStatus.UnderReview => HiringStageKind.Screening,
        ApplicationStatus.Shortlisted => HiringStageKind.Screening,
        ApplicationStatus.InterviewScheduled => HiringStageKind.Interview,
        ApplicationStatus.InterviewCompleted => HiringStageKind.Interview,
        ApplicationStatus.OfferExtended => HiringStageKind.Offer,
        ApplicationStatus.Accepted => HiringStageKind.Hired,
        _ => HiringStageKind.Rejected
    };
}
