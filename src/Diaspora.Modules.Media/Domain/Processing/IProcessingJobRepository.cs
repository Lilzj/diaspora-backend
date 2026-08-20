using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Media.Domain.Processing;

/// <summary>The processing job's persistence boundary.</summary>
public interface IProcessingJobRepository
{
    /// <summary>One job with its steps.</summary>
    Task<ProcessingJob?> GetAsync(
        ProcessingJobId id, CancellationToken cancellationToken = default);

    /// <summary>The most recent job for an asset. What "why is my photo not ready" reads.</summary>
    Task<ProcessingJob?> GetLatestForMediaAsync(
        MediaId mediaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessingJob>> ListForMediaAsync(
        MediaId mediaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Jobs waiting to run, oldest first.
    /// <para>
    /// Oldest first because the alternative starves: under sustained load, newest-first means
    /// the first upload of a busy hour is processed after the last, and the member who waited
    /// longest waits longest.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ProcessingJob>> FindPendingAsync(
        int limit, CancellationToken cancellationToken = default);

    /// <summary>Failed jobs with attempts left. What the retry sweep picks up.</summary>
    Task<IReadOnlyList<ProcessingJob>> FindRetryableAsync(
        int limit, CancellationToken cancellationToken = default);

    Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default);
}
