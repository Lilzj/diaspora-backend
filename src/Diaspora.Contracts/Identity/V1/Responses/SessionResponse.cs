namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// One entry in the member's session list.
/// </summary>
public sealed record SessionResponse
{
    public required Guid Id { get; init; }

    public required string DeviceName { get; init; }

    public required string Platform { get; init; }

    public string? AppVersion { get; init; }

    public string? IpAddress { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string? RevocationReason { get; init; }

    /// <summary>True for the session the request was authenticated with.</summary>
    public required bool IsCurrent { get; init; }
}
