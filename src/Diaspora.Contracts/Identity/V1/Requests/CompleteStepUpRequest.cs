namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Redeems a step-up code and binds the grant it earns to the calling session (BSEC-007).
/// <para>
/// The scope is named again here, and this is the one that matters: it is what the grant will
/// authorise. A code is proof of possession and nothing more — what that proof is spent on is
/// decided at this call, which is why a grant earned for one scope can never satisfy another.
/// </para>
/// </summary>
public sealed record CompleteStepUpRequest
{
    public required string Scope { get; init; }

    public required string Code { get; init; }
}
