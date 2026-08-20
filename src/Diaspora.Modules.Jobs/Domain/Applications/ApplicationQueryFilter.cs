using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// Everything an application listing can narrow on — the employer's applicant list and the
/// candidate's own tracker, which are the same query read from opposite ends.
/// <para>
/// <b>Exactly one of <see cref="EmployerId"/> and <see cref="ApplicantUserId"/> must be set by
/// every caller that is not staff.</b> An application listing scoped to neither is a query that
/// returns other people's job applications, and the repository refuses it rather than trusting
/// callers to remember.
/// </para>
/// </summary>
public sealed record ApplicationQueryFilter
{
    /// <summary>The employer's applicant list. Their whole pipeline, or one posting's.</summary>
    public EmployerProfileId? EmployerId { get; init; }

    /// <summary>The candidate's own tracker.</summary>
    public UserId? ApplicantUserId { get; init; }

    public CandidateProfileId? CandidateId { get; init; }

    /// <summary>Narrow to one posting.</summary>
    public JobPostingId? PostingId { get; init; }

    public IReadOnlyList<ApplicationStatus>? Statuses { get; init; }

    /// <summary>Only applications still under consideration.</summary>
    public bool ActiveOnly { get; init; }

    /// <summary>The employer's pipeline stage. Their vocabulary.</summary>
    public string? StageKey { get; init; }

    /// <summary>Only applications no one at the employer has opened. The neglect view.</summary>
    public bool UnviewedOnly { get; init; }

    /// <summary>
    /// Only applications submitted before this instant and still unanswered. What the
    /// stale-application sweep and the employer's "you are keeping people waiting" nudge run on.
    /// </summary>
    public DateTimeOffset? UnansweredBefore { get; init; }

    public DateTimeOffset? SubmittedAfter { get; init; }

    /// <summary>Newest first when false; oldest first for a "clear the backlog" view.</summary>
    public bool OldestFirst { get; init; }

    /// <summary>Whether the caller holds a platform staff role, and so may query unscoped.</summary>
    public bool IsStaff { get; init; }
}
