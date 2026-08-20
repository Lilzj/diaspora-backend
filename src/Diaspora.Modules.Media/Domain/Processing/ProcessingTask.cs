using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Processing;

/// <summary>
/// One step inside a processing job: extract metadata, scan, generate thumbnails.
/// <para>
/// An entity inside <see cref="ProcessingJob"/>. Its whole reason for existing separately is
/// that a step's outcome is not the job's outcome — a transcode that no registered processor
/// can perform must be distinguishable from one that was attempted and failed, and from one
/// that never applied to this kind of file in the first place.
/// </para>
/// <para>
/// <see cref="Duration"/> is recorded per step because that is where the operational answer
/// lives. "Processing is slow" is not actionable; "the malware scan is taking eleven seconds
/// at the ninety-fifth percentile" is.
/// </para>
/// </summary>
public sealed class ProcessingTask : Entity<ProcessingTaskId>
{
    public const int MaxDetailLength = 500;

    public ProcessingJobId JobId { get; private set; } = default!;

    public ProcessingOperation Operation { get; private set; }

    /// <summary>Execution order within the job. Lower runs first.</summary>
    public int Order { get; private set; }

    public ProcessingTaskStatus Status { get; private set; }

    /// <summary>
    /// What happened, in words. An error message on failure, the name of the processor that
    /// ran on success, the reason nothing claimed the operation when unsupported.
    /// </summary>
    public string? Detail { get; private set; }

    public TimeSpan? Duration { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private ProcessingTask() { }

    private ProcessingTask(
        ProcessingTaskId id, ProcessingJobId jobId, ProcessingOperation operation, int order)
        : base(id)
    {
        JobId = jobId;
        Operation = operation;
        Order = order;
        Status = ProcessingTaskStatus.Pending;
    }

    internal static ProcessingTask Create(
        ProcessingJobId jobId, ProcessingOperation operation, int order)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        return new ProcessingTask(ProcessingTaskId.New(), jobId, operation, order);
    }

    internal void Record(ProcessingTaskStatus status, string? detail, TimeSpan duration)
    {
        Status = status;
        Detail = Truncate(detail, MaxDetailLength);
        Duration = duration;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    internal void Reset()
    {
        Status = ProcessingTaskStatus.Pending;
        Detail = null;
        Duration = null;
        CompletedAt = null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
