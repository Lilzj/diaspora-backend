namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// Trust's verdict on a listing, projected.
/// <para>
/// <see cref="Reason"/> is present on the owner's view only. A refusal reason is how the claimant
/// fixes what was wrong; rendering it to a buyer would publish "we did not believe this seller"
/// against a listing whose paperwork was simply late.
/// </para>
/// </summary>
public sealed record ListingTrustResponse
{
    /// <summary><c>None</c>, <c>Pending</c>, <c>Verified</c>, <c>Rejected</c> or <c>Revoked</c>.</summary>
    public required string State { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust's badge label. Rendered, never interpreted.</summary>
    public string? Badge { get; init; }

    /// <summary>Why Trust refused or withdrew. Owner's view only.</summary>
    public string? Reason { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }
}
