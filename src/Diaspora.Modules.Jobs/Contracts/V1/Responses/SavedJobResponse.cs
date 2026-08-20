namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One saved job, with the card it points at.</summary>
public sealed record SavedJobResponse
{
    public required Guid SavedJobId { get; init; }

    /// <summary>The posting itself, so a saved list renders without a second query.</summary>
    public required JobCardResponse Job { get; init; }

    /// <summary>The candidate's private note. Never visible to the employer.</summary>
    public string? Note { get; init; }

    public required bool RemindBeforeClosing { get; init; }

    public required DateTimeOffset SavedAt { get; init; }
}
