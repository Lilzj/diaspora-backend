namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Asks for a code to confirm a sensitive operation (BSEC-007).
/// <para>
/// The scope is the one the refusal named in its <c>stepUpScope</c> extension. Sending it back
/// here is checked before any message is sent, so a client that names a scope the platform does not
/// have is told at once rather than after the member has read out a code that could not have
/// worked.
/// </para>
/// </summary>
public sealed record RequestStepUpRequest
{
    public required string Scope { get; init; }
}
