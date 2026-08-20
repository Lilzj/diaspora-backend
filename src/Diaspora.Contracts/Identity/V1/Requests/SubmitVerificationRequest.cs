namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Asks to be considered for Diaspora verification. Identity records the request and
/// hands it to the Trust &amp; Verification Engine; it does not decide the outcome.
/// </summary>
public sealed record SubmitVerificationRequest
{
    /// <summary>Optional note from the member to accompany the request.</summary>
    public string? Notes { get; init; }
}
