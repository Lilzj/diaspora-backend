namespace Diaspora.Platform.Operations.Scheduling;

/// <summary>What kind of schedule a job is on.</summary>
public enum JobKind
{
    /// <summary>Run as soon as a worker picks it up.</summary>
    Immediate = 0,

    /// <summary>Run once, at or after a moment.</summary>
    Delayed = 1,

    /// <summary>Run repeatedly, on a cron expression.</summary>
    Recurring = 2
}

/// <summary>Where a job is.</summary>
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,

    /// <summary>Cancelled before it ran, or while running.</summary>
    Cancelled = 4,

    /// <summary>Failed enough times to be given up on. Kept for inspection.</summary>
    DeadLettered = 5
}

/// <summary>
/// How urgently a job should be picked up.
/// <para>
/// Three levels rather than an integer. An integer priority invites forty distinct values that
/// nobody can order consistently, and the scheduler's answer to "which of these two 37s first" is
/// arbitrary anyway.
/// </para>
/// </summary>
public enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

/// <summary>
/// A scheduled unit of work.
/// </summary>
/// <param name="Id">Assigned by the scheduler. Stable for the job's life.</param>
/// <param name="JobType">
/// Names the <see cref="IJobHandler"/> that runs it. A string rather than a <c>Type</c>: a durable
/// scheduler persists this, and a persisted assembly-qualified type name is a migration hazard the
/// first time a class moves namespace.
/// </param>
/// <param name="Payload">Serialized arguments, or null.</param>
/// <param name="Kind">Which schedule.</param>
/// <param name="RunAt">When it next runs. Null for <see cref="JobKind.Immediate"/>.</param>
/// <param name="CronExpression">The recurrence, for <see cref="JobKind.Recurring"/>.</param>
/// <param name="Priority">Pick-up order.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Attempts">How many times it has been tried.</param>
/// <param name="LastError">The last failure's message. Never a stack trace.</param>
/// <param name="CreatedAt">When it was scheduled.</param>
/// <param name="CompletedAt">When it last finished.</param>
/// <param name="CorrelationId">
/// Carried from the request that scheduled it, so a trace does not end at the enqueue. Without this
/// a background job is an orphan in the tracing backend and nobody can answer "what caused this".
/// </param>
public sealed record ScheduledJob(
    string Id,
    string JobType,
    string? Payload,
    JobKind Kind,
    DateTimeOffset? RunAt,
    string? CronExpression,
    JobPriority Priority,
    JobStatus Status,
    int Attempts,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? CorrelationId);

/// <summary>What a handler is given.</summary>
/// <param name="JobId">The job's id.</param>
/// <param name="JobType">Its type.</param>
/// <param name="Payload">Its serialized arguments.</param>
/// <param name="Attempt">1 on the first try.</param>
/// <param name="CorrelationId">The correlation id the job was scheduled under.</param>
public readonly record struct JobContext(
    string JobId,
    string JobType,
    string? Payload,
    int Attempt,
    string? CorrelationId);

/// <summary>
/// Runs one kind of job.
/// <para>
/// Registered by the module that owns the work. The scheduler resolves a handler by
/// <see cref="JobType"/> and knows nothing else about it — which is what keeps a distributed
/// scheduler from needing a reference to every module that schedules something.
/// </para>
/// </summary>
public interface IJobHandler
{
    /// <summary>The type name this handler answers to. Unique across the platform.</summary>
    string JobType { get; }

    /// <summary>
    /// Runs the job.
    /// <para>
    /// <b>Must be idempotent.</b> A distributed scheduler delivers at least once — a worker that
    /// completes a job and dies before recording the completion will hand it to another worker — and
    /// no scheduler on any platform can honestly promise otherwise.
    /// </para>
    /// </summary>
    Task ExecuteAsync(JobContext context, CancellationToken cancellationToken = default);
}
