namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// The guide's verification mark — this module's projection of Trust's decision. Nothing in
/// B8 can grant it; the only path to <c>Verified</c> is a Trust integration event.
/// </summary>
public sealed record GuideTrustMarkResponse
{
    /// <summary>A <c>GuideVerificationState</c> name — <c>None</c>, <c>Pending</c>, <c>Verified</c>, <c>Revoked</c>.</summary>
    public required string State { get; init; }

    /// <summary>The badge Trust granted, when it granted one.</summary>
    public string? Badge { get; init; }

    /// <summary>Why the mark is what it is. Populated for the author, never for readers.</summary>
    public string? Reason { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }
}
