using Diaspora.Platform.Operations.Scheduling;

namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// The platform's job scheduler.
/// <para>
/// <b>B17 closed DEBT-014 and rewrote this port in the same commit</b>, for the same reasons and with
/// the same absence of consequence as <see cref="ICacheService"/>: declared since B1, implemented
/// never, depended on by nothing, and pinned by an architecture rule that existed to keep it that way.
/// </para>
/// <para>
/// The shape it carried could not have been implemented as written. <c>EnqueueAsync&lt;T&gt;(T job)</c>
/// says what to run but not who runs it, and <c>AddRecurringAsync&lt;T&gt;(id, cron)</c> passes a type
/// with no instance and no handler contract anywhere — so a scheduler would have had to invent a
/// convention for resolving work from a type, persist an assembly-qualified name, and hope nothing
/// ever moved namespace. The replacement names the handler explicitly through
/// <see cref="IJobHandler.JobType"/>, which is a string a durable store can hold safely.
/// </para>
/// <para>
/// <b>At-least-once, always.</b> Every method here schedules work that may run more than once. A
/// handler that is not idempotent will eventually double-charge somebody, and no scheduler design
/// available to this platform can prevent that — only the handler can.
/// </para>
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>Schedules a job to run as soon as a worker is free.</summary>
    /// <param name="jobType">The <see cref="IJobHandler.JobType"/> that will run it.</param>
    /// <param name="payload">Serialized arguments, or null.</param>
    /// <param name="priority">Pick-up order.</param>
    /// <returns>The job's id.</returns>
    Task<string> EnqueueAsync(
        string jobType,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>Schedules a job to run once, after a delay.</summary>
    Task<string> ScheduleAsync(
        string jobType,
        TimeSpan delay,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>Schedules a job to run once, at a moment.</summary>
    Task<string> ScheduleAtAsync(
        string jobType,
        DateTimeOffset runAt,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or replaces a recurring job.
    /// <para>
    /// Keyed on <paramref name="id"/> rather than on the job type, so one job type can have several
    /// recurrences and re-registering at startup is idempotent — which it must be, because every
    /// replica registers the same recurring jobs every time it starts.
    /// </para>
    /// </summary>
    Task AddRecurringAsync(
        string id,
        string jobType,
        string cronExpression,
        string? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a recurring job. Does nothing if it was never registered.</summary>
    Task RemoveRecurringAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending job, and requests cancellation of a running one.
    /// </summary>
    /// <returns>False when the job is unknown or already finished.</returns>
    Task<bool> CancelAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>One job's current state, or null.</summary>
    Task<ScheduledJob?> FindAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>Jobs in a given state, newest first. For the operational dashboard.</summary>
    Task<IReadOnlyList<ScheduledJob>> ListAsync(
        JobStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
