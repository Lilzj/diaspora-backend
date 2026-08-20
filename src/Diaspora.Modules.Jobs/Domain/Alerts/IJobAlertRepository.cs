using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Alerts;

/// <summary>Reads and writes standing job alerts.</summary>
public interface IJobAlertRepository
{
    Task<JobAlert?> GetAsync(JobAlertId id, CancellationToken cancellationToken = default);

    /// <summary>Every alert this member holds, active and paused.</summary>
    Task<IReadOnlyList<JobAlert>> GetForUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>How many this member holds. Enforces the ceiling.</summary>
    Task<int> CountForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active alerts whose watermark is old enough to sweep, tracked and bounded.
    /// <para>
    /// Bounded deliberately: the sweep runs over every candidate on the platform, and a pass
    /// that loaded them all would be one query that grows without limit. The sweep takes
    /// batches and advances each alert's own watermark, so an interrupted pass resumes exactly
    /// where it stopped.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<JobAlert>> GetDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(JobAlert alert, CancellationToken cancellationToken = default);

    void Remove(JobAlert alert);
}
