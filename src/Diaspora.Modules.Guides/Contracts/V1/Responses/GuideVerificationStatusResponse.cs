namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// Where the guide's verification stands. The strongest thing a submission returns is
/// <c>Pending</c> — outcomes arrive later, through Trust's integration events.
/// </summary>
public sealed record GuideVerificationStatusResponse
{
    public required Guid GuideId { get; init; }

    /// <summary>A <c>GuideVerificationState</c> name.</summary>
    public required string State { get; init; }

    /// <summary>The deciding system's case identifier, when a claim is or was open.</summary>
    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    /// <summary>Why the mark is what it is. For the author.</summary>
    public string? Reason { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>When the state last changed — the decision's timestamp, not the write's.</summary>
    public DateTimeOffset? ChangedAt { get; init; }
}
