using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Processing;

namespace Diaspora.Modules.Media.Application.Mapping;

/// <summary>Projects processing jobs onto their wire contract.</summary>
public static class ProcessingMapper
{
    public static ProcessingJobResponse ToResponse(ProcessingJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new ProcessingJobResponse
        {
            JobId = job.Id.Value,
            MediaId = job.MediaId.Value,
            Status = MediaWireNames.ToWire(job.Status),
            Attempts = job.Attempts,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            FailureReason = job.FailureReason,
            Tasks = job.Tasks
                .OrderBy(task => task.Order)
                .Select(ToTask)
                .ToList()
        };
    }

    private static ProcessingTaskResponse ToTask(ProcessingTask task) =>
        new()
        {
            Operation = MediaWireNames.ToWire(task.Operation),
            Status = MediaWireNames.ToWire(task.Status),
            Detail = task.Detail,
            DurationMilliseconds = task.Duration?.TotalMilliseconds
        };
}
