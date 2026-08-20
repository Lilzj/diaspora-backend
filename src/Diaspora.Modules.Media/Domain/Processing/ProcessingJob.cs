using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Events;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.Processing;

/// <summary>
/// The post-upload work over one asset, and the record of how each piece of it went.
/// <para>
/// <b>Why the outcome is per-step rather than per-job.</b> A deployment with no video codec
/// still extracts metadata, still scans, still strips EXIF; only the transcode is missing.
/// Collapsing that into a single job status would report "failed" for an asset that is
/// perfectly usable, and an operator would have no way to tell it apart from a genuine
/// failure. Recording each step separately makes "thumbnails generated, transcode unsupported,
/// EXIF stripped" a readable state — and makes <see cref="ProcessingJobStatus.Skipped"/>
/// distinguishable from <see cref="ProcessingJobStatus.Failed"/>, which is the difference
/// between a missing capability and a broken one.
/// </para>
/// <para>
/// Retry is bounded and counted on the job. An unbounded retry over a file that will never
/// decode is a queue that never drains.
/// </para>
/// </summary>
public sealed class ProcessingJob : AggregateRoot<ProcessingJobId>, IAuditableEntity
{
    /// <summary>
    /// How many times a failed job is retried. Three because the failures worth retrying are
    /// transient — a storage blip, a scanner restart — and a fourth attempt at a file that
    /// genuinely will not decode is just latency in the queue for everybody else.
    /// </summary>
    public const int MaxAttempts = 3;

    private readonly List<ProcessingTask> _tasks = [];

    public MediaId MediaId { get; private set; } = default!;

    public ProcessingJobStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<ProcessingTask> Tasks => _tasks.AsReadOnly();

    /// <summary>Steps still to run, in declaration order.</summary>
    public IReadOnlyList<ProcessingTask> PendingTasks =>
        _tasks.Where(task => task.Status is ProcessingTaskStatus.Pending)
            .OrderBy(task => task.Order)
            .ToList()
            .AsReadOnly();

    public bool CanRetry => Status is ProcessingJobStatus.Failed && Attempts < MaxAttempts;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private ProcessingJob() { }

    private ProcessingJob(ProcessingJobId id, MediaId mediaId) : base(id)
    {
        MediaId = mediaId;
        Status = ProcessingJobStatus.Pending;
    }

    /// <summary>
    /// Plans the work for an asset.
    /// <para>
    /// The order is not cosmetic. Signature validation runs first because everything after it
    /// assumes the file is what it claims; scanning runs second because nothing should decode
    /// bytes that have not been scanned; and metadata extraction precedes the renditions
    /// because a rendition needs the source dimensions to know whether it would be an
    /// upscale.
    /// </para>
    /// </summary>
    public static ProcessingJob Plan(MediaId mediaId, IReadOnlyList<ProcessingOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        ArgumentNullException.ThrowIfNull(operations);

        var job = new ProcessingJob(ProcessingJobId.New(), mediaId);

        var order = 0;

        foreach (var operation in operations.Distinct())
        {
            job._tasks.Add(ProcessingTask.Create(job.Id, operation, order++));
        }

        return job;
    }

    public Result Begin(DateTimeOffset now)
    {
        if (Status is ProcessingJobStatus.Running)
        {
            return Result.Success();
        }

        Status = ProcessingJobStatus.Running;
        StartedAt ??= now;
        Attempts++;

        return Result.Success();
    }

    /// <summary>Records how one step went.</summary>
    public Result RecordTask(
        ProcessingOperation operation,
        ProcessingTaskStatus status,
        string? detail,
        TimeSpan duration)
    {
        var task = _tasks.FirstOrDefault(candidate => candidate.Operation == operation);

        if (task is null)
        {
            return Result.Failure(Domain.Errors.MediaErrors.ProcessingJobNotFound);
        }

        task.Record(status, detail, duration);
        return Result.Success();
    }

    /// <summary>
    /// Closes the job, deriving its status from its steps.
    /// <para>
    /// The derivation is the interesting part. Any hard failure fails the job. Everything
    /// unsupported or not-applicable means the job was <see cref="ProcessingJobStatus.Skipped"/>
    /// — an absence of capability, not an error. A mix of success and unsupported is
    /// <see cref="ProcessingJobStatus.PartiallySucceeded"/>: the asset is usable, something is
    /// missing, and an operator can see exactly what.
    /// </para>
    /// </summary>
    public Result Complete(DateTimeOffset now)
    {
        CompletedAt = now;

        if (_tasks.Count == 0)
        {
            Status = ProcessingJobStatus.Succeeded;
            return Result.Success();
        }

        var failed = _tasks.Count(task => task.Status is ProcessingTaskStatus.Failed);
        var succeeded = _tasks.Count(task => task.Status is ProcessingTaskStatus.Succeeded);
        var unavailable = _tasks.Count(task =>
            task.Status is ProcessingTaskStatus.Unsupported or ProcessingTaskStatus.NotApplicable);

        Status = (failed, succeeded, unavailable) switch
        {
            ( > 0, _, _) => ProcessingJobStatus.Failed,
            (0, 0, _) => ProcessingJobStatus.Skipped,
            (0, _, > 0) => ProcessingJobStatus.PartiallySucceeded,
            _ => ProcessingJobStatus.Succeeded
        };

        if (Status is ProcessingJobStatus.Failed)
        {
            FailureReason = _tasks
                .Where(task => task.Status is ProcessingTaskStatus.Failed)
                .Select(task => $"{task.Operation}: {task.Detail}")
                .FirstOrDefault();
        }

        RaiseDomainEvent(new ProcessingJobCompletedDomainEvent
        {
            JobId = Id,
            MediaId = MediaId,
            Status = Status,
            FailedTaskCount = failed,
            UnsupportedTaskCount = unavailable
        });

        return Result.Success();
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        Status = ProcessingJobStatus.Failed;
        FailureReason = reason.Length <= 500 ? reason : reason[..500];
        CompletedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status is ProcessingJobStatus.Succeeded or ProcessingJobStatus.PartiallySucceeded)
        {
            return;
        }

        Status = ProcessingJobStatus.Cancelled;
        CompletedAt = now;
    }

    /// <summary>Reopens a failed job for another attempt, resetting only its failed steps.</summary>
    public Result Requeue()
    {
        if (!CanRetry)
        {
            return Result.Failure(Domain.Errors.MediaErrors.ProcessingJobNotFound);
        }

        foreach (var task in _tasks.Where(task => task.Status is ProcessingTaskStatus.Failed))
        {
            task.Reset();
        }

        Status = ProcessingJobStatus.Pending;
        FailureReason = null;
        CompletedAt = null;

        return Result.Success();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
