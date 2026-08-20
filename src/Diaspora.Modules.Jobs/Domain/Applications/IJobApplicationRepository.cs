using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// Reads and writes applications.
/// <para>
/// The listing read deliberately does not load the history: an applicant list of twenty rows
/// showing status and submission date must not drag several hundred history entries with it.
/// The detail read does, because the timeline is the reason anyone opens one.
/// </para>
/// </summary>
public interface IJobApplicationRepository
{
    /// <summary>The full aggregate, tracked, history included. For commands.</summary>
    Task<JobApplication?> GetAsync(
        JobApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the detail screen and the timeline.</summary>
    Task<JobApplication?> GetForReadAsync(
        JobApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member who applied, and nothing else. Null when the application is gone.
    /// <para>
    /// <b>Its own method rather than a field off <see cref="GetAsync"/>, for the reason stated
    /// at the top of this interface.</b> Every published contract about an application has to
    /// name the candidate as a member, and materialising an unbounded history to read one
    /// identifier — on every status change, interview and offer on the platform — is exactly
    /// the read this repository is careful not to make anywhere else.
    /// </para>
    /// </summary>
    Task<UserId?> FindApplicantUserIdAsync(
        JobApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// This candidate's application to this posting, if any. Tracked.
    /// <para>
    /// The duplicate check, and the reason it is a dedicated method: "have I already applied"
    /// is asked on every posting detail render and on every submission, and expressing it as a
    /// filtered query would make the hottest read in the module a scan of the candidate's whole
    /// history.
    /// </para>
    /// </summary>
    Task<JobApplication?> GetByCandidateAndPostingAsync(
        CandidateProfileId candidateId,
        JobPostingId postingId,
        CancellationToken cancellationToken = default);

    /// <summary>The listing. Employer applicant list and candidate tracker alike, no history.</summary>
    Task<CursorPagedResult<JobApplication>> QueryAsync(
        ApplicationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many applications match. Dashboard tiles and pipeline counts.</summary>
    Task<int> CountAsync(
        ApplicationQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applications on one posting, grouped by status. The hiring-pipeline view, answered in
    /// one round trip rather than one per column.
    /// </summary>
    Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same breakdown across a whole employer. The dashboard's funnel, and the figure the
    /// employer's conversion rate is computed from.
    /// </summary>
    Task<IReadOnlyDictionary<ApplicationStatus, int>> CountByStatusForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default);

    /// <summary>Applications by pipeline stage on one posting. The kanban board's counts.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByStageAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applications left unanswered past the staleness threshold, tracked and bounded. What the
    /// ghosting sweep works from — the figure that goes on an employer's public reputation, so
    /// it is measured rather than asserted.
    /// </summary>
    Task<IReadOnlyList<JobApplication>> GetStaleUnansweredAsync(
        DateTimeOffset submittedBefore, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live application on a posting, tracked. What a posting closure or an employer
    /// suspension has to walk to tell the candidates.
    /// </summary>
    Task<IReadOnlyList<JobApplication>> GetActiveForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>How many applications this member has sent since an instant. The abuse bound.</summary>
    Task<int> CountByApplicantSinceAsync(
        UserId applicantUserId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// The postings this member has applied to, from a candidate set. Lets a results page
    /// annotate "you applied" without one query per card.
    /// </summary>
    Task<IReadOnlySet<Guid>> FindAppliedPostingIdsAsync(
        UserId applicantUserId,
        IReadOnlyCollection<JobPostingId> postingIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(JobApplication application, CancellationToken cancellationToken = default);

    void Remove(JobApplication application);
}
