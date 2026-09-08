namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One request to take money off the platform.
/// <para>
/// <b>Three amounts rather than two and an assumption.</b> The gross left the balance, the fee stayed
/// with the platform and the net is what arrives; a client computing the third from the other two
/// would disagree with the server at a rounding boundary, and the number it would get wrong is the
/// one the member cares about.
/// </para>
/// <para>
/// <b>The destination is a server-issued identifier and a masked label.</b> No account number, no
/// IBAN and no sort code has ever been in this contract — the platform does not hold one.
/// </para>
/// </summary>
public sealed record WithdrawalResponse
{
    public required Guid WithdrawalId { get; init; }

    /// <summary>
    /// <c>Requested</c>, <c>Processing</c>, <c>Completed</c>, <c>Failed</c>, <c>Cancelled</c> or
    /// <c>Reversed</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>What left the member's balance.</summary>
    public required MoneyResponse Gross { get; init; }

    /// <summary>What the platform kept. Zero when this deployment charges nothing.</summary>
    public required MoneyResponse Fee { get; init; }

    /// <summary>What the member actually receives.</summary>
    public required MoneyResponse Net { get; init; }

    public required Guid DestinationId { get; init; }

    /// <summary>What a person recognises the destination by. Never the credential.</summary>
    public required string DestinationLabel { get; init; }

    /// <summary>Whether the member may still call it off. False once a provider has taken it.</summary>
    public required bool IsCancellable { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ProcessingStartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Where a member's money goes when it leaves.
/// <para>
/// <b>There is no field here that could carry an account number, and there is no field on the
/// aggregate behind it either.</b> §10 of the frontend's own brief asked for that property
/// structurally rather than by discipline, and this is what that looks like: a server-issued id, a
/// kind, and a label somebody produced for a person to read.
/// </para>
/// </summary>
public sealed record PayoutDestinationResponse
{
    public required Guid DestinationId { get; init; }

    /// <summary><c>BankAccount</c>, <c>MobileMoney</c>, <c>Card</c> or <c>ProviderManaged</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>"Barclays ••••4417". The whole of what this platform knows.</summary>
    public required string Label { get; init; }

    public required string Currency { get; init; }

    /// <summary>Nothing pays out to an unverified destination.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>Whether money may be sent here right now.</summary>
    public required bool IsUsable { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }
}

/// <summary>
/// Where to send a member to finish setting up a payout account.
/// <para>
/// <b>A URL rather than a form, and that is the whole design.</b> The member types their bank
/// details into the provider's own pages; no credential passes through this platform, so there is
/// nothing here to fill in. What comes back is a destination that exists and is not yet payable.
/// </para>
/// </summary>
public sealed record PayoutOnboardingResponse
{
    /// <summary>False when the provider refused. <see cref="Reason"/> says why, in the member's words.</summary>
    public required bool Started { get; init; }

    /// <summary>
    /// The provider's hosted setup page. Open it, do not render it.
    /// <para>
    /// Single-use and short-lived — a link kept and reopened later is spent. Ask again rather than
    /// storing this.
    /// </para>
    /// </summary>
    public string? Url { get; init; }

    /// <summary>The destination this opened, recorded before the member has finished.</summary>
    public Guid? DestinationId { get; init; }

    public string? Provider { get; init; }

    /// <summary>Why they cannot start. Absent when they can.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// True when this country is served by a provider that wants an identifier, not a setup page.
    /// <para>
    /// <b>Two shapes of "add an account", and only the matrix knows which one applies.</b> Stripe
    /// Connect hands the member to its own hosted pages and gives back an account reference; PayPal
    /// pays to an address the member already has — an email, a phone number — and has no setup to
    /// hand off to. A client that assumed one shape would be wrong in half the world.
    /// </para>
    /// <para>
    /// When true, <see cref="Url"/> is absent and nothing has been created: the client collects the
    /// identifier and posts it to <c>/payout-destinations/identity</c>.
    /// </para>
    /// </summary>
    public bool RequiresIdentity { get; init; }

    /// <summary>What to ask the member for, in their words. "your PayPal email".</summary>
    public string? IdentityHint { get; init; }
}
