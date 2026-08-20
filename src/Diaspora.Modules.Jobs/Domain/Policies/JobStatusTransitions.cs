using Diaspora.Modules.Jobs.Domain.Enums;

namespace Diaspora.Modules.Jobs.Domain.Policies;

/// <summary>
/// The legal moves through a posting's lifecycle, declared once.
/// <para>
/// <b>A table rather than a chain of <c>if</c> statements inside the aggregate.</b> The posting
/// has nine states and a dozen mutators, and expressing the machine inline means the rule
/// "a filled posting never reopens" is written in six places and true in five. Declared here it
/// is written once, testable without constructing an aggregate, and printable into the
/// documentation the hiring-workflow guide needs.
/// </para>
/// <para>
/// <b>Two exits are available from almost anywhere and are not in the table.</b> Expiry is the
/// clock's, not a person's, and archival is the employer's right at any point; both are guarded
/// by the aggregate directly. What the table governs is forward progress through hiring, which
/// is the part where getting it wrong loses a candidate.
/// </para>
/// </summary>
public static class JobStatusTransitions
{
    private static readonly Dictionary<JobStatus, JobStatus[]> Allowed = new()
    {
        // A draft goes live, or is abandoned.
        [JobStatus.Draft] =
        [
            JobStatus.Published,
            JobStatus.Open,
            JobStatus.Archived
        ],

        // Published but not yet taking applications — the advertise-early case.
        [JobStatus.Published] =
        [
            JobStatus.Open,
            JobStatus.Draft,
            JobStatus.Expired,
            JobStatus.Archived
        ],

        // Open. The first application moves it on by itself.
        [JobStatus.Open] =
        [
            JobStatus.ReceivingApplications,
            JobStatus.Interviewing,
            JobStatus.Filled,
            JobStatus.Expired,
            JobStatus.Archived
        ],

        [JobStatus.ReceivingApplications] =
        [
            JobStatus.Interviewing,
            JobStatus.OfferMade,
            JobStatus.Filled,
            JobStatus.Expired,
            JobStatus.Archived
        ],

        [JobStatus.Interviewing] =
        [
            JobStatus.OfferMade,
            JobStatus.ReceivingApplications,
            JobStatus.Filled,
            JobStatus.Expired,
            JobStatus.Archived
        ],

        // An offer can fall through, and the posting goes back to interviewing rather than
        // to a dead end. This is the single most common real correction in hiring.
        [JobStatus.OfferMade] =
        [
            JobStatus.Filled,
            JobStatus.Interviewing,
            JobStatus.Expired,
            JobStatus.Archived
        ],

        // Filled is terminal but for archival. A seat that opens again is a new posting —
        // reopening would silently rewrite the history of everyone who applied to the first.
        [JobStatus.Filled] =
        [
            JobStatus.Archived
        ],

        // Expiry is reversible: an employer who extends the closing date reopens it.
        [JobStatus.Expired] =
        [
            JobStatus.Open,
            JobStatus.Archived
        ],

        // Archived returns to whatever it can sensibly resume as.
        [JobStatus.Archived] =
        [
            JobStatus.Draft,
            JobStatus.Open
        ]
    };

    /// <summary>Whether a posting may move from one status to another.</summary>
    public static bool CanTransition(JobStatus from, JobStatus to) =>
        from != to
        && Allowed.TryGetValue(from, out var targets)
        && Array.IndexOf(targets, to) >= 0;

    /// <summary>Everything a posting in this status may become. For the documentation and the client.</summary>
    public static IReadOnlyList<JobStatus> From(JobStatus status) =>
        Allowed.TryGetValue(status, out var targets) ? targets : [];

    /// <summary>Whether a posting in this status is accepting applications.</summary>
    public static bool AcceptsApplications(JobStatus status) =>
        status is JobStatus.Open or JobStatus.ReceivingApplications or JobStatus.Interviewing;

    /// <summary>Whether a posting in this status may appear on a public surface.</summary>
    public static bool IsPubliclyVisible(JobStatus status) =>
        status is not (JobStatus.Draft or JobStatus.Archived);

    /// <summary>Whether hiring on this posting has ended, one way or another.</summary>
    public static bool IsTerminal(JobStatus status) =>
        status is JobStatus.Filled or JobStatus.Archived;
}
