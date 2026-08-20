namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>Saves a job to come back to.</summary>
public sealed record SaveJobRequest
{
    /// <summary>The candidate's private note. Never visible to the employer.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Remind me before it closes. The one thing that turns a saved list into something that
    /// actually helps somebody apply in time.
    /// </summary>
    public bool RemindBeforeClosing { get; init; }
}
