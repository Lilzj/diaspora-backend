namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// One entry from the member's security history.
/// </summary>
public sealed record SecurityEventResponse
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Severity { get; init; }

    public string? Description { get; init; }

    public string? IpAddress { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
