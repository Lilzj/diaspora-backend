using System.Collections.Concurrent;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Scheduling;

/// <summary>
/// The shipped scheduler: in-process, correct for one instance, and explicit about what it is not.
/// <para>
/// <b>Jobs do not survive a restart.</b> A pending job in this scheduler is a row in a dictionary,
/// and a process that stops loses every one of them. That is stated first because it is the property
/// that decides what may be scheduled: work whose loss is acceptable, or work something else will
/// re-derive. <b>Anything that must happen goes through the outbox</b>, which is durable, transactional
/// and already carries the platform's important asynchronous work.
/// </para>
/// <para>
/// The seam is the deliverable. <see cref="IBackgroundJobScheduler"/> now has a shape a durable
/// implementation can satisfy — a string job type rather than a generic, an explicit handler
/// contract, a cron expression that parses at registration — which is what DEBT-014 was actually
/// blocking. A store-backed scheduler replaces this class and nothing that schedules a job changes.
/// </para>
/// </summary>
public sealed class InMemoryJobScheduler : IBackgroundJobScheduler
{
    private readonly ConcurrentDictionary<string, JobRecord> _jobs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RecurringRegistration> _recurring = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly IGuidGenerator _guids;
    private readonly ICorrelationContext? _correlation;
    private readonly ILogger<InMemoryJobScheduler> _logger;

    public InMemoryJobScheduler(
        IClock clock,
        IGuidGenerator guids,
        ILogger<InMemoryJobScheduler> logger,
        ICorrelationContext? correlation = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(guids);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _guids = guids;
        _logger = logger;
        _correlation = correlation;
    }

    public Task<string> EnqueueAsync(
        string jobType,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Add(jobType, payload, JobKind.Immediate, _clock.UtcNow, null, priority));

    public Task<string> ScheduleAsync(
        string jobType,
        TimeSpan delay,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delay.Ticks);

        return Task.FromResult(
            Add(jobType, payload, JobKind.Delayed, _clock.UtcNow + delay, null, priority));
    }

    public Task<string> ScheduleAtAsync(
        string jobType,
        DateTimeOffset runAt,
        string? payload = null,
        JobPriority priority = JobPriority.Normal,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Add(jobType, payload, JobKind.Delayed, runAt, null, priority));

    public Task AddRecurringAsync(
        string id,
        string jobType,
        string cronExpression,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);

        // Parsed here rather than at the first tick, so a bad expression fails the startup that
        // registered it instead of silently never firing.
        var cron = CronExpression.Parse(cronExpression);
        var next = cron.GetNextOccurrence(_clock.UtcNow);

        if (next is null)
        {
            throw new ArgumentException(
                $"'{cronExpression}' has no occurrence within the next 400 days, so registering it " +
                "would schedule nothing.", nameof(cronExpression));
        }

        _recurring[id] = new RecurringRegistration(id, jobType, cron, payload, next.Value);

        _logger.LogInformation(
            "Recurring job {JobId} ({JobType}) registered on '{Cron}'. Next run {Next:O}.",
            id, jobType, cron.Expression, next.Value);

        return Task.CompletedTask;
    }

    public Task RemoveRecurringAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _recurring.TryRemove(id, out _);

        return Task.CompletedTask;
    }

    public Task<bool> CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        lock (job.Gate)
        {
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed or
                JobStatus.Cancelled or JobStatus.DeadLettered)
            {
                return Task.FromResult(false);
            }

            job.Status = JobStatus.Cancelled;
            job.CompletedAt = _clock.UtcNow;

            // Signals a running handler. A pending job simply never starts; a running one is asked to
            // stop and its handler decides how quickly it can.
            job.Cancellation.Cancel();
        }

        return Task.FromResult(true);
    }

    public Task<ScheduledJob?> FindAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        return Task.FromResult(_jobs.TryGetValue(jobId, out var job) ? job.ToScheduledJob() : null);
    }

    public Task<IReadOnlyList<ScheduledJob>> ListAsync(
        JobStatus? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        IReadOnlyList<ScheduledJob> jobs =
        [
            .. _jobs.Values
                .Where(job => status is null || job.Status == status)
                .OrderByDescending(job => job.CreatedAt)
                .Take(limit)
                .Select(job => job.ToScheduledJob())
        ];

        return Task.FromResult(jobs);
    }

    // ─── Consumed by the job worker ──────────────────────────────────────────────────────

    /// <summary>
    /// Claims the next runnable job, highest priority and oldest first, or null.
    /// <para>
    /// Claiming marks it running under its own lock, so two workers in the same process cannot take
    /// one job. Two workers in two processes can, because this scheduler's state is per-process —
    /// which is the property the durable implementation exists to fix.
    /// </para>
    /// </summary>
    internal JobRecord? Claim()
    {
        var now = _clock.UtcNow;

        foreach (var job in _jobs.Values
            .Where(candidate => candidate.Status == JobStatus.Pending && candidate.RunAt <= now)
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.RunAt))
        {
            lock (job.Gate)
            {
                if (job.Status != JobStatus.Pending)
                {
                    continue;
                }

                job.Status = JobStatus.Running;
                job.Attempts++;

                return job;
            }
        }

        return null;
    }

    /// <summary>Promotes every recurring registration whose moment has arrived into a pending job.</summary>
    internal int PromoteDueRecurring()
    {
        var now = _clock.UtcNow;
        var promoted = 0;

        foreach (var registration in _recurring.Values)
        {
            if (registration.NextRunAt > now)
            {
                continue;
            }

            // Advanced before the job is added. The other order would let a slow add produce a second
            // promotion of the same occurrence on the next tick.
            var next = registration.Cron.GetNextOccurrence(now);

            if (next is null)
            {
                _logger.LogWarning(
                    "Recurring job {JobId} has no further occurrence and has been removed.", registration.Id);

                _recurring.TryRemove(registration.Id, out _);
                continue;
            }

            registration.NextRunAt = next.Value;

            Add(registration.JobType, registration.Payload, JobKind.Recurring, now,
                registration.Cron.Expression, JobPriority.Normal);

            promoted++;
        }

        return promoted;
    }

    internal void Complete(JobRecord job, Exception? failure, int poisonThreshold)
    {
        ArgumentNullException.ThrowIfNull(job);

        lock (job.Gate)
        {
            if (job.Status == JobStatus.Cancelled)
            {
                return;
            }

            if (failure is null)
            {
                job.Status = JobStatus.Succeeded;
                job.LastError = null;
            }
            else if (job.Attempts >= poisonThreshold)
            {
                job.Status = JobStatus.DeadLettered;
                job.LastError = failure.Message;

                _logger.LogError(
                    failure,
                    "Job {JobId} ({JobType}) failed {Attempts} time(s) and was dead-lettered.",
                    job.Id, job.JobType, job.Attempts);
            }
            else
            {
                // Back to pending with a delay, so a failing job does not spin. The delay is the
                // attempt count in seconds, capped: enough to break a tight loop, short enough that a
                // transient failure recovers within a cycle or two.
                job.Status = JobStatus.Pending;
                job.LastError = failure.Message;
                job.RunAt = _clock.UtcNow + TimeSpan.FromSeconds(Math.Min(job.Attempts * 5, 300));
            }

            job.CompletedAt = _clock.UtcNow;
        }
    }

    /// <summary>Discards finished jobs older than <paramref name="olderThan"/>.</summary>
    internal int Prune(TimeSpan olderThan)
    {
        var cutoff = _clock.UtcNow - olderThan;
        var removed = 0;

        foreach (var entry in _jobs)
        {
            var job = entry.Value;

            // Dead-lettered jobs are kept. They are the ones an operator needs to look at, and a
            // retention sweep that discarded them would delete the evidence of the only failures
            // anybody cares about.
            if (job.Status is JobStatus.Succeeded or JobStatus.Cancelled &&
                job.CompletedAt < cutoff &&
                _jobs.TryRemove(new KeyValuePair<string, JobRecord>(entry.Key, job)))
            {
                removed++;
            }
        }

        return removed;
    }

    internal IReadOnlyList<RecurringRegistration> Recurring => [.. _recurring.Values];

    private string Add(
        string jobType,
        string? payload,
        JobKind kind,
        DateTimeOffset runAt,
        string? cron,
        JobPriority priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);

        var id = _guids.NewGuid().ToString("N");

        _jobs[id] = new JobRecord
        {
            Id = id,
            JobType = jobType,
            Payload = payload,
            Kind = kind,
            RunAt = runAt,
            CronExpression = cron,
            Priority = priority,
            Status = JobStatus.Pending,
            CreatedAt = _clock.UtcNow,

            // Captured at schedule time. Reading it when the job runs would give the worker's
            // correlation id — a fresh one per iteration — and the causal link to the request that
            // scheduled the work would be lost exactly where it is most useful.
            CorrelationId = _correlation?.CorrelationId
        };

        return id;
    }

    internal sealed class JobRecord
    {
        public Lock Gate { get; } = new();

        public CancellationTokenSource Cancellation { get; } = new();

        public required string Id { get; init; }

        public required string JobType { get; init; }

        public string? Payload { get; init; }

        public JobKind Kind { get; init; }

        public DateTimeOffset RunAt { get; set; }

        public string? CronExpression { get; init; }

        public JobPriority Priority { get; init; }

        public JobStatus Status { get; set; }

        public int Attempts { get; set; }

        public string? LastError { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? CompletedAt { get; set; }

        public string? CorrelationId { get; init; }

        public ScheduledJob ToScheduledJob() => new(
            Id, JobType, Payload, Kind, RunAt, CronExpression, Priority, Status,
            Attempts, LastError, CreatedAt, CompletedAt, CorrelationId);
    }

    internal sealed class RecurringRegistration
    {
        public RecurringRegistration(
            string id, string jobType, CronExpression cron, string? payload, DateTimeOffset nextRunAt)
        {
            Id = id;
            JobType = jobType;
            Cron = cron;
            Payload = payload;
            NextRunAt = nextRunAt;
        }

        public string Id { get; }

        public string JobType { get; }

        public CronExpression Cron { get; }

        public string? Payload { get; }

        public DateTimeOffset NextRunAt { get; set; }
    }
}
