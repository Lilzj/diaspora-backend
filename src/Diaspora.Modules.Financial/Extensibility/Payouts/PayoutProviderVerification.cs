namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// Where a payee has got to with one provider's own onboarding.
/// <para>
/// <b>This is not the platform's verification and must never be confused with it.</b> The platform's
/// record is <c>FinancialAccount.Status</c>, event-sourced from Trust, and it answers "do we know
/// who this person is". This answers a different question — "has this person completed the checks
/// <em>this provider</em> requires before it will move money to them" — and a member can be fully
/// verified by Trust and still have done nothing with a provider they have never been routed to.
/// </para>
/// </summary>
public enum PayoutProviderKycStatus
{
    /// <summary>
    /// Nothing has been started, or nothing can report on it. The default, and it fails closed:
    /// a route that requires onboarding is refused rather than attempted.
    /// </summary>
    NotStarted = 0,

    /// <summary>Submitted and being reviewed. Not yet payable.</summary>
    Pending = 1,

    /// <summary>The provider will accept payouts to this payee.</summary>
    Verified = 2,

    /// <summary>The provider refused. Retrying without new information spends nothing usefully.</summary>
    Rejected = 3,

    /// <summary>
    /// Verified once and since limited — a document that needs replacing, a threshold crossed, a
    /// sanctions hit under review. Distinct from <see cref="Rejected"/> because it usually clears.
    /// </summary>
    Restricted = 4,

    /// <summary>Verification lapsed. Payable again once refreshed.</summary>
    Expired = 5
}

/// <summary>
/// One provider's answer about one payee.
/// <para>
/// <b>Carries what an operator needs to unblock somebody, not just a yes or no.</b> A member whose
/// payout refuses wants to know which document is missing; a support agent wants the provider's own
/// reference so they can look it up in that provider's console. A bare boolean would send both of
/// them to the logs.
/// </para>
/// <para>
/// <b>Nothing here is a credential.</b> <see cref="Reference"/> is the provider's identifier for its
/// own record — the same category as a payout destination's token — and the documents themselves
/// never reach this platform.
/// </para>
/// </summary>
public sealed record PayoutProviderVerification
{
    /// <summary>The adapter this answer is about.</summary>
    public required string Provider { get; init; }

    public required PayoutProviderKycStatus Status { get; init; }

    /// <summary>
    /// What the provider is still waiting for, in its own words. Empty when it is waiting for
    /// nothing. Shown to operators; never parsed.
    /// </summary>
    public IReadOnlyList<string> OutstandingRequirements { get; init; } = [];

    /// <summary>The provider's handle for its own verification record. Never a credential.</summary>
    public string? Reference { get; init; }

    /// <summary>When this answer was established. Null when nothing has been asked.</summary>
    public DateTimeOffset? LastCheckedAt { get; init; }

    /// <summary>When it lapses, where the provider says. Null when it does not expire or does not say.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool IsPayable => Status == PayoutProviderKycStatus.Verified;

    /// <summary>The answer for a provider nobody can ask. Fails closed — see the enum summary.</summary>
    public static PayoutProviderVerification Unknown(string provider) =>
        new() { Provider = provider, Status = PayoutProviderKycStatus.NotStarted };
}

/// <summary>
/// Where a provider's onboarding state is read from.
/// <para>
/// <b>A seam rather than a table, because the state belongs to the provider and not to this
/// platform.</b> A connected account's requirements change when the provider changes them — a new
/// document, a threshold crossed, a jurisdiction rule — and a row in this schema would be a copy
/// that is wrong at exactly the moment somebody is refused. An adapter answers from its own SDK, and
/// caches behind this interface if the provider's rate limits require it.
/// </para>
/// <para>
/// <b>No implementation ships.</b> Like <c>ISettlementProvider</c> and <c>IExchangeRateProvider</c>,
/// this is declared so the day an adapter lands there is somewhere for it to plug in. Until then a
/// capability row marked <c>RequiresProviderOnboarding</c> refuses, which is the safe direction and
/// keeps the requirement visible in the matrix rather than silently satisfied.
/// </para>
/// </summary>
public interface IPayoutProviderVerificationSource
{
    /// <summary>The adapter this source answers for. Matched against a capability's provider.</summary>
    string Provider { get; }

    /// <summary>
    /// This payee's standing with this provider. Implementations return
    /// <see cref="PayoutProviderVerification.Unknown"/> rather than throwing when they cannot
    /// establish it: "we could not ask" and "they are not onboarded" both mean do not pay, and a
    /// throw here would take down a sweep processing other people's money.
    /// </summary>
    Task<PayoutProviderVerification> GetAsync(
        Guid payeeUserId, CancellationToken cancellationToken = default);
}
