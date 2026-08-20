namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>The state of one step inside a processing job.</summary>
public enum ProcessingTaskStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,

    /// <summary>No registered processor claimed the operation in this deployment.</summary>
    Unsupported = 4,

    /// <summary>Not applicable to this asset — a poster frame for a PDF.</summary>
    NotApplicable = 5
}
