namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Add money to your own balance.
/// <para>
/// <b>There is no wallet identifier here, and there never will be.</b> A deposit credits the primary
/// wallet on the account the intent is opened against, resolved server-side from the caller. A
/// destination in a request body is a destination somebody would eventually point at somebody else.
/// </para>
/// </summary>
public sealed record CreateDepositRequest
{
    /// <summary>In major units, like every other request DTO in this module.</summary>
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string MethodKind { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>Uniquely indexed. A double-tapped button produces one deposit.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Take money off the platform.
/// <para>
/// The amount is the gross — what leaves the balance. The fee and the net are decided by the server
/// and returned; a client that sent either would be a client pricing its own withdrawal.
/// </para>
/// </summary>
public sealed record CreateWithdrawalRequest
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>A destination the caller already registered and had verified.</summary>
    public required Guid DestinationId { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// Required here or in the <c>Idempotency-Key</c> header, which wins. Unique per member: a retry
    /// with the same key answers with the first withdrawal.
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Register somewhere money can be sent.
/// <para>
/// <b>Carries no credential, and this is the point rather than an omission.</b> The account number
/// goes to the settlement provider through its own SDK, which returns a token and a label; those are
/// what this platform stores. §10's prohibition on client-side payout credentials is met by the
/// contract having nowhere to put one.
/// </para>
/// </summary>
public sealed record RegisterPayoutDestinationRequest
{
    /// <summary><c>BankAccount</c>, <c>MobileMoney</c>, <c>Card</c> or <c>ProviderManaged</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>What a person recognises it by. "Barclays ••••4417".</summary>
    public required string Label { get; init; }

    public required string Currency { get; init; }

    /// <summary>The settlement provider's own token for the details it holds.</summary>
    public string? ProviderToken { get; init; }

    public string? Provider { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>Ask for a refund. Nothing moves until a staff member agrees.</summary>
/// <summary>
/// Starting a payout provider ’s onboarding.
/// <para>
/// <b>No credential, by design.</b> The member types their bank details into the provider ’s own
/// pages; this carries only where they are and what they will be paid in, which is what the provider
/// needs to open the right kind of account.
/// </para>
/// </summary>
public sealed record StartPayoutOnboardingRequest
{
    public required string Currency { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The provider opens the account here and cannot move it later.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// Where the provider returns the member. Absent uses the application ’s own deep link.
    /// <para>
    /// Supplied by the client rather than fixed here so a web build and a device build can each name
    /// somewhere that exists for them.
    /// </para>
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// An address a payout provider already knows the member by.
/// <para>
/// <b>Not a credential, which is why it may be typed.</b> An email or a phone number names where
/// money should arrive, the way a postal address does; it cannot move anything on its own. An
/// account number can, and never appears in any request this platform accepts.
/// </para>
/// <para>
/// The provider is not named here. Which one an address is registered against is the payout matrix's
/// answer for the member's country, because a client naming it would be a client-supplied claim
/// about where somebody's money goes.
/// </para>
/// </summary>
public sealed record RegisterPayoutIdentityRequest
{
    public required string Identity { get; init; }

    public required string Currency { get; init; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }
}

public sealed record CreateRefundRequestRequest
{
    /// <summary>
    /// How much to ask for. Omit for the whole refundable remainder — which is what most members
    /// mean, and asking them to type a figure they would have to compute is asking for a mistake.
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// <c>RequestedByPayer</c>, <c>Duplicate</c>, <c>NotDelivered</c>. A member-choosable subset;
    /// <c>Fraudulent</c> and <c>Goodwill</c> are staff decisions and are refused here.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>What happened, in the member's own words. Shown to whoever decides.</summary>
    public required string Note { get; init; }
}

/// <summary>
/// Answer a member's refund request. Staff.
/// <para>
/// <b>An approval issues the refund in the same change.</b> The handler calls the payment
/// processor and attaches the resulting refund to the request before it returns, so there is no
/// second step and no window in which an approved request is waiting on one. This doc said the
/// opposite until 19 September 2026 — on the record a client reads, about a button that moves
/// money — which is the sort of mistake that gets a queue worked twice.
/// </para>
/// </summary>
public sealed record DecideRefundRequestRequest
{
    /// <summary>True to approve, which refunds the payer.</summary>
    public required bool Approve { get; init; }

    /// <summary>A member-safe explanation. It is shown to them.</summary>
    public string? DecisionNote { get; init; }
}

/// <summary>Turn reward points into spendable credit.</summary>
public sealed record ConvertRewardPointsRequest
{
    public required long Points { get; init; }

    /// <summary>
    /// Optional. The credit is in the currency the published rate is in; naming any other currency
    /// is refused (<c>financial.rewards.currency_not_offered</c>) rather than repriced.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Ignored, and kept only so existing clients still bind. The rate is chosen by the country on the
    /// member's own financial account.
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>Uniquely indexed. A double-tapped button converts once.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Award points to a member by hand. Staff only, and bounded by the <c>reward.grant</c> policy: per
/// grant, and per staff member per rolling day.
/// </summary>
public sealed record GrantRewardPointsRequest
{
    public required Guid UserId { get; init; }

    public required long Points { get; init; }

    /// <summary>Why. <c>ReferralQualified</c>, <c>GuidePublished</c>, <c>SeasonalBonus</c>.</summary>
    public required string Source { get; init; }

    /// <summary>
    /// The grantor's own handle for the thing being rewarded. Uniquely indexed per account, so two
    /// grants carrying one reference award points once.
    /// </summary>
    public required string GrantReference { get; init; }

    /// <summary>Why. Required, and stored on the grant with the staff member who made it.</summary>
    public required string Reason { get; init; }
}

/// <summary>Hold a member's points from conversion, or lift the hold. Staff.</summary>
public sealed record SetRewardConversionHoldRequest
{
    /// <summary>True to hold, false to lift.</summary>
    public required bool IsHeld { get; init; }

    /// <summary>Why. Required both ways; kept with the decision and never shown to the member.</summary>
    public required string Reason { get; init; }
}

/// <summary>Change plan, mid-period.</summary>
public sealed record ChangeSubscriptionPlanRequest
{
    public required string PlanCode { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Renew a placement. Extends the window rather than opening a second one.</summary>
public sealed record RenewPlacementRequest
{
    /// <summary>Omit to buy the same product again, which is what renewing usually means.</summary>
    public string? ProductCode { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Ask for a placement to renew itself when its window closes.</summary>
public sealed record SetAutoRenewRequest
{
    public required bool Enabled { get; init; }
}
