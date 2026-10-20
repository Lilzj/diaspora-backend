namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The address on a member's account and whether they have proven they receive mail there.
/// <para>
/// Shaped to the client's existing <c>EmailIdentityState</c> (FRONTEND-PHASE16-GAP-008), so the
/// app switches from its development adapter by changing where it reads, not what it reads.
/// </para>
/// </summary>
public sealed record EmailIdentityResponse
{
    /// <summary>Empty when the account has no address.</summary>
    public required string Address { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>When the latest link was sent, in this response only. Null on a plain read.</summary>
    public DateTimeOffset? VerificationSentAt { get; init; }

    public DateTimeOffset? VerificationExpiresAt { get; init; }

    /// <summary>Seconds until another link may be requested. Zero when one may be requested now.</summary>
    public required int CooldownSeconds { get; init; }

    /// <summary>
    /// Always <c>ok</c> today. Bounce and complaint webhooks from the email provider are not wired,
    /// so the platform cannot yet know an address is failing, and says so by never claiming otherwise.
    /// </summary>
    public required string DeliveryState { get; init; }

    /// <summary>
    /// The address a change was just requested to, in the response to <c>PUT /auth/email</c> only.
    /// Null on a plain read: a pending change is a signed link in the new mailbox, not a row, so the
    /// server does not remember it. The address on the account stays <see cref="Address"/> until
    /// the link is confirmed.
    /// </summary>
    public string? PendingAddress { get; init; }
}

/// <summary>What opening a verification link did.</summary>
public sealed record EmailVerificationOutcomeResponse
{
    /// <summary><c>Verified</c>, <c>AlreadyVerified</c>, <c>AddressChanged</c> or <c>Invalid</c>.</summary>
    public required string Outcome { get; init; }
}

/// <summary>What opening an email change link did.</summary>
public sealed record EmailChangeOutcomeResponse
{
    /// <summary>
    /// <c>Changed</c>; <c>AlreadyChanged</c> (this link was used); <c>AddressTaken</c> (another
    /// account signs in with the new address); or <c>Invalid</c> (forged, expired, superseded by
    /// another change, or cancelled by a password change).
    /// </summary>
    public required string Outcome { get; init; }
}
