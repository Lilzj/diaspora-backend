namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Applies a moderation decision to a property.</summary>
public sealed record ModerateHousingRequest
{
    /// <summary>Hide, Suspend, Clear, Archive or Note.</summary>
    public required string Action { get; init; }

    /// <summary>Recorded on the append-only moderation trail and on the lifecycle event.</summary>
    public string? Reason { get; init; }
}
