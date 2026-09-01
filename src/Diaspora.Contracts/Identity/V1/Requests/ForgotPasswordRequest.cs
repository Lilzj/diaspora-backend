namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Starts account recovery. The response is identical whether or not the number is
/// registered, so this endpoint cannot be used to enumerate accounts.
/// </summary>
public sealed record ForgotPasswordRequest
{
    /// <summary>
    /// The address the account was registered with.
    /// </summary>
    /// <remarks>
    /// The member identifies themselves by the credential they actually use — their
    /// email — but the recovery code is delivered by <b>SMS to the registered phone</b>,
    /// because this deployment has no configured email sender
    /// (<c>UnconfiguredEmailSender</c> is the only registration). The response says so,
    /// naming the masked number, so nobody waits on an inbox that will never receive it.
    /// <para>
    /// Recovery is rare, so this is a handful of messages rather than one per sign-in.
    /// </para>
    /// </remarks>
    public required string Email { get; init; }
}
