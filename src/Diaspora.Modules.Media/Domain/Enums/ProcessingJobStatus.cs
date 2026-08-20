namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>The state of a post-upload processing job.</summary>
public enum ProcessingJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,

    /// <summary>Some steps succeeded, some did not. The asset is usable; something is missing.</summary>
    PartiallySucceeded = 3,

    Failed = 4,

    /// <summary>Every step was unsupported in this deployment. Not a failure — an absence.</summary>
    Skipped = 5,

    Cancelled = 6
}
