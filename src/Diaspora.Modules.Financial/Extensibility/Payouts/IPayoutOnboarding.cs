namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// Taking a member through a payout provider's own onboarding.
/// <para>
/// <b>The missing half of a payout destination.</b> <c>PayoutDestination.Register</c> records a
/// destination and leaves it unverified, and <c>Verify</c> is staff-only because verification is the
/// provider's — a micro deposit, a name match, an open-banking handshake. Nothing stood between
/// those two, so every member's destination stayed unusable and the withdraw screen was a dead end
/// for everybody.
/// </para>
/// <para>
/// <b>No credential passes through this platform.</b> The member finishes onboarding on the
/// provider's own hosted pages; what comes back here is an account reference, never an account
/// number. That is the same rule <c>registerPayoutDestination</c> states on the client, and it is
/// why this returns a URL to send somebody to rather than a form to fill in.
/// </para>
/// <para>
/// <b>Declared per provider.</b> A deployment paying out through Stripe in one country and
/// Flutterwave in another registers one of these each, and the router picks by the capability
/// matrix — the same way <see cref="IPayoutProviderVerificationSource"/> is chosen.
/// </para>
/// </summary>
public interface IPayoutOnboarding
{
    /// <summary>The adapter this onboarding belongs to. Matched against a capability's provider.</summary>
    string Provider { get; }

    /// <summary>
    /// Opens or resumes onboarding and answers where to send the member.
    /// <para>
    /// Refuses rather than throws for everything a member could hit — a country the provider will
    /// not open an account in, a provider the platform has not enabled — because each is a different
    /// sentence on screen and none of them is an outage.
    /// </para>
    /// </summary>
    Task<PayoutOnboardingSession> StartAsync(
        PayoutOnboardingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Who is onboarding, where they are, and where to send them back to.</summary>
public sealed record PayoutOnboardingRequest
{
    public required Guid PayeeUserId { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The provider opens the account in this country and cannot move it.</summary>
    public required string CountryCode { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// An account the member already started.
    /// <para>
    /// Passed so a member who abandoned onboarding resumes it rather than opening a second account
    /// with the provider. Two half-finished accounts for one person is a support conversation
    /// nobody can unpick from this side.
    /// </para>
    /// </summary>
    public string? ExistingAccountReference { get; init; }

    /// <summary>Where the provider sends the member when they finish.</summary>
    public required string ReturnUrl { get; init; }

    /// <summary>Where the provider sends them when the link has expired and they need a new one.</summary>
    public required string RefreshUrl { get; init; }

    /// <summary>Pre-fills the provider's form. Optional, and never required.</summary>
    public string? Email { get; init; }
}

/// <summary>Where to send the member, or why they cannot go.</summary>
public sealed record PayoutOnboardingSession
{
    public required bool Started { get; init; }

    /// <summary>The provider's hosted onboarding page. Present only when <see cref="Started"/>.</summary>
    public string? Url { get; init; }

    /// <summary>
    /// The provider's handle for the account, stored as the destination's token.
    /// <para>
    /// Present even when the member has not finished: the account exists from the moment onboarding
    /// opens, and losing the reference would strand it. It is what
    /// <c>IPayoutProviderVerificationSource</c> is later asked about.
    /// </para>
    /// </summary>
    public string? AccountReference { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public static PayoutOnboardingSession Ready(string url, string accountReference) => new()
    {
        Started = true,
        Url = url,
        AccountReference = accountReference
    };

    /// <summary>
    /// The member cannot start, and the reason is theirs to read.
    /// <para>
    /// Carries the account reference where one exists, so an account opened before the refusal is
    /// still recorded rather than orphaned at the provider.
    /// </para>
    /// </summary>
    public static PayoutOnboardingSession Refused(
        string code, string message, string? accountReference = null) => new()
    {
        Started = false,
        FailureCode = code,
        FailureMessage = message,
        AccountReference = accountReference
    };
}
