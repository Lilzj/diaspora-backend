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

/// <summary>Answer a member's refund request. Staff.</summary>
public sealed record DecideRefundRequestRequest
{
    /// <summary>True to approve. An approval does not itself move money.</summary>
    public required bool Approve { get; init; }

    /// <summary>A member-safe explanation. It is shown to them.</summary>
    public string? DecisionNote { get; init; }
}

/// <summary>Turn reward points into spendable credit.</summary>
public sealed record ConvertRewardPointsRequest
{
    public required long Points { get; init; }

    /// <summary>Which currency to be credited in. Must be one the caller holds a wallet in.</summary>
    public required string Currency { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>Uniquely indexed. A double-tapped button converts once.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Award points to a member. Staff, or an integration event from a grantor.</summary>
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
