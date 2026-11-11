namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// A step-up grant the calling session now holds (BSEC-007). Never carries the code that earned it.
/// </summary>
public sealed record StepUpGrantResponse
{
    /// <summary>What this grant authorises. Only requests in this scope proceed on it.</summary>
    public required string Scope { get; init; }

    /// <summary>
    /// When it stops working. The client should retry the guarded operation well inside this, and
    /// treat a second <c>Identity.StepUp.Required</c> as "ask for another code" rather than as an
    /// error — a grant can expire between being issued and being spent.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
