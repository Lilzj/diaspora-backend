using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Withdrawals;

/// <summary>
/// Takes money off the platform.
/// <para>
/// <b>Transactional, because the request and the hold behind it must land together.</b> A withdrawal
/// row with no hold is money a member can spend twice; a hold with no withdrawal is money nobody can
/// reach. The unit of work is what makes neither possible.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "The request and the ledger hold behind it land together or not at all.")]
[Auditable(Action = "financial.withdrawal.create")]
public sealed record CreateWithdrawalCommand : ICommand<WithdrawalResponse>
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required Guid DestinationId { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Calls off a withdrawal nobody has picked up yet, and releases the hold.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "Cancelling the request and releasing its hold are one change.")]
[Auditable(Action = "financial.withdrawal.cancel")]
public sealed record CancelWithdrawalCommand : ICommand<WithdrawalResponse>
{
    public required Guid WithdrawalId { get; init; }
}

/// <summary>The caller's payout requests.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyWithdrawalsQuery : IQuery<IReadOnlyList<WithdrawalResponse>>
{
    public string? Status { get; init; }

    public DateTimeOffset? Before { get; init; }

    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>One payout request of the caller's.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetWithdrawalQuery : IQuery<WithdrawalResponse>
{
    public required Guid WithdrawalId { get; init; }
}

/// <summary>What the caller may withdraw, what it would cost, and what they hold.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetWithdrawalLimitsQuery : IQuery<MovementLimitsResponse>
{
    public string? Currency { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Registers somewhere money can be sent.
/// <para>
/// The request carries a label and a provider token, never a credential — see
/// <c>RegisterPayoutDestinationRequest</c>. This module could not store one if it were sent.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.register")]
public sealed record RegisterPayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required string Kind { get; init; }

    public required string Label { get; init; }

    public required string Currency { get; init; }

    public string? Provider { get; init; }

    public string? ProviderToken { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>The caller's payout destinations.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyPayoutDestinationsQuery : IQuery<IReadOnlyList<PayoutDestinationResponse>>;

/// <summary>Stops using a destination. Never deletes it — old withdrawals still name it.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.remove")]
public sealed record RemovePayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required Guid DestinationId { get; init; }
}

/// <summary>
/// Opens or resumes the payout provider — own onboarding, and answers where to send the member.
/// <para>
/// <b>The member — command, not staff —.</b> Registering a destination and verifying one already
/// existed, and nothing joined them: a member could not add an account at all, and a destination
/// arrived unverified with only a staff route out of it. This starts the provider flow that ends in
/// a verifiable account.
/// </para>
/// <para>
/// <b>It records the destination before the member has finished.</b> The provider opens the account
/// the moment onboarding starts, and the reference to it has to be stored then or it is lost — a
/// member who abandons the form and returns would otherwise open a second account with the same
/// provider. The destination is unverified and says so; <see cref="RefreshPayoutDestinationCommand"/>
/// is what turns it usable.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.onboard")]
public sealed record StartPayoutOnboardingCommand : ICommand<PayoutOnboardingResponse>
{
    public required string Currency { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The provider opens the account here and cannot move it later.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Where the provider returns the member when they finish. The client — deep link.</summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Records an address a provider already knows the member by.
/// <para>
/// <b>The other half of adding an account, and it is a form rather than a hand-off.</b> A PayPal
/// payout goes to an email, a phone number or a PayPal id — things the member already has and can
/// simply tell us. There is no hosted setup to send them to, so
/// <see cref="StartPayoutOnboardingCommand"/> answers <c>RequiresIdentity</c> and the client posts
/// here instead.
/// </para>
/// <para>
/// <b>An address is not a credential.</b> Nothing here can move money on its own: it names where a
/// payout should arrive, the way a postal address does. That is why it may be typed into the
/// application when an account number may never be.
/// </para>
/// <para>
/// <b>The label is produced here, not accepted.</b> A client-supplied label is a client-supplied
/// claim about somebody's money, and the response contract has always described it as the server's.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.identity")]
public sealed record RegisterPayoutIdentityCommand : ICommand<PayoutDestinationResponse>
{
    /// <summary>The address itself — an email, a phone number, a provider id.</summary>
    public required string Identity { get; init; }

    public required string Currency { get; init; }

    /// <summary>ISO 3166-1 alpha-2. Decides which provider this may be registered against.</summary>
    public required string CountryCode { get; init; }
}

/// <summary>
/// Asks the provider whether a destination is payable yet, and records the answer.
/// <para>
/// <b>The member — own route to being verified.</b> <see cref="VerifyPayoutDestinationCommand"/>
/// is staff recording that somebody who could check did. This asks the provider directly through
/// <c>IPayoutProviderVerificationSource</c>, so a member who has just finished onboarding does not
/// wait on a human for something Stripe already knows.
/// </para>
/// <para>
/// <b>It can only verify, never unverify.</b> A provider that answers "not yet" leaves the
/// destination exactly as it was. Withdrawing a verification on a transient read — a rate limit, a
/// timeout — would strand money mid-payout for a reason nobody could see.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.refresh")]
public sealed record RefreshPayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required Guid DestinationId { get; init; }
}

/// <summary>
/// Marks a destination verified. Staff.
/// <para>
/// Verification itself is the settlement provider's — a micro deposit, a name match, an open-banking
/// handshake. B15 records only that somebody who could check did, and refuses to pay out until they
/// have.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletManage)]
[Auditable(Action = "financial.payout_destination.verify")]
public sealed record VerifyPayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required Guid DestinationId { get; init; }

    public string? Provider { get; init; }

    public string? ProviderToken { get; init; }
}
