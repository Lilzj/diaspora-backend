using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Reports;

/// <summary>The report's persistence boundary.</summary>
public interface IJobReportRepository
{
    Task<JobReport?> GetAsync(JobReportId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The moderation queue, worst first.
    /// <para>
    /// Ordered by whether the reason is one where money is being asked of a candidate, then by
    /// age. A queue in plain date order buries a fee demand filed this morning under a fortnight
    /// of duplicates.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<JobReport>> ListAsync(
        bool openOnly,
        bool fraudFirst,
        JobPostingId? postingId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Every open report about one posting, so a decision can close them together.</summary>
    Task<IReadOnlyList<JobReport>> GetOpenForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>Whether this member has already reported this posting. One member, one report.</summary>
    Task<bool> HasReportedAsync(
        JobPostingId postingId, UserId reportedBy, CancellationToken cancellationToken = default);

    /// <summary>How many distinct members have reported it. What the flag threshold counts.</summary>
    Task<int> CountForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upheld reports against this employer, across every advert they have placed.
    /// <para>
    /// The pattern is the signal in recruitment fraud: three upheld fee demands from one employer
    /// is a different thing from three unrelated one-offs, and a moderator looking at a single
    /// card cannot see the difference.
    /// </para>
    /// </summary>
    Task<int> CountUpheldForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default);

    Task AddAsync(JobReport report, CancellationToken cancellationToken = default);
}
