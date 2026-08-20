namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What the platform did with a callback.
/// <para>
/// <b>Deliberately says almost nothing.</b> A caller that has not proved it is the provider learns
/// only that the request was refused; a caller that has learns which outcome was recorded so its own
/// dashboard can show it. No payment identifier, no amount, no member — a webhook endpoint is an
/// unauthenticated surface and the response body is the easiest place to leak from one.
/// </para>
/// </summary>
public sealed record WebhookAcknowledgementResponse
{
    /// <summary>
    /// One of <c>Unverified</c>, <c>Ignored</c>, <c>Unmatched</c>, <c>Duplicate</c>, <c>Applied</c>
    /// or <c>Refused</c>.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>True when this callback had already been recorded and was replayed.</summary>
    public required bool WasDuplicate { get; init; }

    /// <summary>
    /// A short, non-identifying reason. Never the payload, never an amount and never a member.
    /// </summary>
    public string? Detail { get; init; }
}
