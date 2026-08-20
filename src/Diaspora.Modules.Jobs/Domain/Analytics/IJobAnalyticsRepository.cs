using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Analytics;

/// <summary>Reads and writes per-posting analytics rollups.</summary>
public interface IJobAnalyticsRepository
{
    /// <summary>One posting's rollup, tracked.</summary>
    Task<JobAnalytics?> GetForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>One posting's rollup, untracked. For the dashboard read.</summary>
    Task<JobAnalytics?> GetForPostingForReadAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several rollups at once, untracked. An employer dashboard listing twenty postings with
    /// their funnels is one query, not twenty.
    /// </summary>
    Task<IReadOnlyList<JobAnalytics>> GetManyAsync(
        IReadOnlyCollection<JobPostingId> postingIds, CancellationToken cancellationToken = default);

    /// <summary>Every rollup for an employer, untracked. The employer-wide funnel.</summary>
    Task<IReadOnlyList<JobAnalytics>> GetForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default);

    Task AddAsync(JobAnalytics analytics, CancellationToken cancellationToken = default);
}
