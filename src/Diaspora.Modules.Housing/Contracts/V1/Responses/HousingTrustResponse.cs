namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// What Trust has said. Read from the cached mark; nothing in Housing writes it.
/// </summary>
public sealed record HousingTrustResponse
{
    /// <summary>Unverified, Pending, Verified, Rejected or Revoked.</summary>
    public required string State { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>The badge Trust granted, verbatim. Housing never invents one.</summary>
    public string? Badge { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }
}
