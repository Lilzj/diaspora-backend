namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B15.1 Giving Extension — the wire contracts for COMMUNITY-LIFE-GAP-003, -006, -007 and -008.
//
// Shaped to the frontend's Phase 12 domain model, field for field, because the point of closing a
// gap is that the client's simulated adapter is deleted and nothing else moves.
//
// Two properties hold throughout:
//
//   Every financial figure is the server's. `percentage` is computed here and there is no
//   arithmetic anywhere on the device that divides a raised by a target. `isReached` on a milestone
//   is a stamp the server made from the cause's own total, never a comparison a client could redo
//   and disagree about.
//
//   An anonymised record carries no identity to hide. An anonymous supporter arrives with no user
//   id, no name, and nothing that correlates two of their gifts — not an identity beside a flag
//   saying "please do not show this".
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The cursor-paged envelope every B15.1 list uses.</summary>
public sealed record GivingPageResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}

// ── GAP-003 · Membership payment binding ─────────────────────────────────────────────────────────

/// <summary>
/// What the platform now knows about the money behind a membership.
/// <para>
/// <b>Every field here is B15's read of its own record.</b> Nothing is echoed back from the request:
/// the payer is the intent's payer, the amount is what was captured, and the status is what the
/// intent says now — not what the caller believed when they asked.
/// </para>
/// </summary>
public sealed record MembershipPaymentBindingResponse
{
    public required Guid BindingId { get; init; }

    public required Guid MembershipId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    /// <summary>The intent's current status. <c>Captured</c> or <c>Settled</c> — nothing else binds.</summary>
    public required string PaymentStatus { get; init; }

    public required MoneyResponse Amount { get; init; }

    /// <summary>When the platform judged the money taken.</summary>
    public required DateTimeOffset SettledAt { get; init; }

    public required DateTimeOffset BoundAt { get; init; }

    /// <summary>
    /// False when this call found the binding already there.
    /// <para>
    /// A retried request is answered with the binding that exists rather than an error, and this
    /// flag is how a caller tells the two apart without the status code changing.
    /// </para>
    /// </summary>
    public required bool IsNewBinding { get; init; }
}

// ── GAP-006 · Campaigns ──────────────────────────────────────────────────────────────────────────

/// <summary>The money side of a campaign, entirely server-computed.</summary>
public sealed record CampaignProgressResponse
{
    /// <summary>Null where the campaign set no target. Never a zero standing in for one.</summary>
    public MoneyResponse? Target { get; init; }

    /// <summary>B15's own figure, from the linked cause. Zero before publication, because nothing has been given.</summary>
    public required MoneyResponse Raised { get; init; }

    /// <summary>
    /// 0–100, computed here.
    /// <para>
    /// Null where there is no target, because a percentage of nothing is not zero — it is
    /// undefined, and a client rendering a 0% bar for an untargeted campaign would be stating
    /// something false.
    /// </para>
    /// </summary>
    public int? Percentage { get; init; }

    public required int SupporterCount { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    /// <summary>Server-decided. The client does not subtract dates to decide urgency.</summary>
    public int? DaysRemaining { get; init; }
}

public sealed record CampaignResponse
{
    public required Guid CampaignId { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Story { get; init; }

    /// <summary>A Media reference, never a URL. Resolve it through Media as on every other surface.</summary>
    public string? CoverImage { get; init; }

    /// <summary>Media references, never URLs.</summary>
    public required IReadOnlyList<string> Gallery { get; init; }

    public required string Category { get; init; }

    public required string Status { get; init; }

    public required CampaignProgressResponse Progress { get; init; }

    /// <summary>
    /// The B15 cause this campaign collects through. Null before publication.
    /// <para>
    /// Its absence is the contract: a campaign with no cause has nowhere certified to collect, and
    /// the client draws no donate button for one.
    /// </para>
    /// </summary>
    public string? CauseCode { get; init; }

    public required string BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public required string BeneficiaryName { get; init; }

    public required string Urgency { get; init; }

    public required IReadOnlyList<string> AcceptedKinds { get; init; }

    public required Guid OrganizerId { get; init; }

    public required string OrganizerName { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Trust's word about the organiser, projected through the cause. False before publication.</summary>
    public required bool IsVerified { get; init; }

    public string? CountryCode { get; init; }

    public string? LocationLabel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Set when the status is <c>Rejected</c>.</summary>
    public string? ReviewNote { get; init; }

    /// <summary>What the caller may do, decided here. Never inferred by the client from a role.</summary>
    public required bool CanManage { get; init; }

    /// <summary>
    /// Whether money may be given right now.
    /// <para>
    /// Both halves of the rule: a state that collects <em>and</em> a linked cause. This is the field
    /// the detail screen branches on.
    /// </para>
    /// </summary>
    public required bool CanDonate { get; init; }

    /// <summary>
    /// The lifecycle moves legal from here, for the caller who may make them.
    /// <para>
    /// Empty for somebody who does not manage the campaign. The client's own transition table draws
    /// buttons; this is the server saying which of them it would actually accept.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> AvailableTransitions { get; init; }
}

// ── GAP-007 · Transparency ───────────────────────────────────────────────────────────────────────

public sealed record CampaignUpdateResponse
{
    public required Guid UpdateId { get; init; }

    public required Guid CampaignId { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>Media references, never URLs.</summary>
    public required IReadOnlyList<string> MediaReferences { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required string AuthorName { get; init; }

    /// <summary>Only ever true on the organizer's own read. A supporter never sees a withdrawn update.</summary>
    public required bool IsWithdrawn { get; init; }
}

/// <summary>
/// A funding figure and whether it has been passed.
/// <para>
/// <b><c>isReached</c> is the server's stamp, not a comparison.</b> The client is given the answer,
/// never the two numbers to divide.
/// </para>
/// </summary>
public sealed record CampaignMilestoneResponse
{
    public required Guid MilestoneId { get; init; }

    public required string Label { get; init; }

    public required MoneyResponse Target { get; init; }

    public required bool IsReached { get; init; }

    public DateTimeOffset? ReachedAt { get; init; }
}

/// <summary>
/// One supporter, already anonymised where anonymity was asked for.
/// <para>
/// <b>An anonymous supporter has no identifier here at all — not a null beside a real one
/// elsewhere.</b> B15 never stored one, so there is nothing to withhold. Neither is there any
/// correlation handle: two anonymous gifts from one person are indistinguishable from two people,
/// which is what anonymity means.
/// </para>
/// </summary>
public sealed record CampaignSupporterResponse
{
    /// <summary>Absent for an anonymous supporter.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// <c>"Anonymous"</c> for an anonymous supporter, and null for a named one.
    /// <para>
    /// <b>Null is the honest answer and a placeholder would not be.</b> B15 stores no donor name —
    /// a donation carries an identifier or nothing at all — and this platform has no seam that turns
    /// a user id into a name; Community's own comment surface returns <c>authorName: null</c> beside
    /// an <c>authorId</c> for exactly the same reason, and the client resolves it. Sending "A
    /// Diaspora member" for every real supporter would put a name the platform invented on a public
    /// list. The register asks only that an <em>anonymous</em> supporter arrive with a placeholder,
    /// and that is what it gets.
    /// </para>
    /// </summary>
    public string? DisplayName { get; init; }

    public required bool IsAnonymous { get; init; }

    /// <summary>Absent where the supporter's amount is not published.</summary>
    public MoneyResponse? Amount { get; init; }

    /// <summary>Absent where they left none, and always absent for an anonymous gift.</summary>
    public string? Message { get; init; }

    public required DateTimeOffset GivenAt { get; init; }
}

/// <summary>One line of what a campaign says it will do with the money, and what it reports it did.</summary>
public sealed record FundUsageLineResponse
{
    public required Guid Id { get; init; }

    public required string Label { get; init; }

    public required MoneyResponse Allocated { get; init; }

    /// <summary>Null where nothing has been spent against this line. Never a zero standing in for it.</summary>
    public MoneyResponse? Spent { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// Whether a settled B15 payout backs the spend, rather than the organizer's own report.
    /// <para>
    /// The client renders the difference. A figure somebody typed and a figure the platform can
    /// prove are not the same claim, and presenting them identically would be inventing one.
    /// </para>
    /// </summary>
    public required bool IsPlatformVerified { get; init; }
}

/// <summary>
/// Where a campaign's money is.
/// <para>
/// <b>Four figures with four different authorities, and the response says which is which.</b>
/// <c>raised</c> is B15's, from the cause's own donations. <c>allocated</c> is the organizer's
/// declaration of intent. <c>disbursed</c> is the sum of what the lines report spent, of which only
/// the platform-verified part is a figure this platform stands behind. <c>remaining</c> is
/// <c>raised</c> less <c>disbursed</c>, computed here.
/// </para>
/// </summary>
public sealed record CampaignFundUsageResponse
{
    public required Guid CampaignId { get; init; }

    public string? CauseCode { get; init; }

    /// <summary>B15's figure. Authoritative.</summary>
    public required MoneyResponse Raised { get; init; }

    /// <summary>The organizer's declared plan. A statement of intent, not of fact.</summary>
    public required MoneyResponse Allocated { get; init; }

    /// <summary>What the lines report spent, verified and reported together.</summary>
    public required MoneyResponse Disbursed { get; init; }

    /// <summary>Of <see cref="Disbursed"/>, the part a settled B15 payout backs.</summary>
    public required MoneyResponse DisbursedVerified { get; init; }

    /// <summary>Raised less disbursed. Never negative; an over-report is clamped and flagged.</summary>
    public required MoneyResponse Remaining { get; init; }

    public required IReadOnlyList<FundUsageLineResponse> Lines { get; init; }

    /// <summary>
    /// True when the lines report spending more than the cause ever raised.
    /// <para>
    /// Surfaced rather than hidden: the figures are the organizer's and the platform will not
    /// quietly adjust them, but a supporter reading the page is entitled to know they do not add up.
    /// </para>
    /// </summary>
    public required bool IsOverReported { get; init; }
}

/// <summary>One bucket of a cause's giving. A UTC day.</summary>
public sealed record DonationTimeSeriesPointResponse
{
    public required DateTimeOffset PeriodStart { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required int DonationCount { get; init; }
}

/// <summary>
/// A cause's giving over a window, bucketed and totalled by the server.
/// <para>
/// <b>Every day in the window is present, including the ones with nothing on them.</b> A series
/// that omitted empty days would let a client draw a line straight from Monday to Friday and call
/// the middle of it Wednesday's giving.
/// </para>
/// </summary>
public sealed record DonationTimeSeriesResponse
{
    public required string CauseCode { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>Always <c>UTC</c>. Stated rather than assumed — see the class summary on the query.</summary>
    public required string TimeZone { get; init; }

    public required string Granularity { get; init; }

    public required IReadOnlyList<DonationTimeSeriesPointResponse> Points { get; init; }

    /// <summary>Completed giving over the whole window.</summary>
    public required MoneyResponse Total { get; init; }

    /// <summary>Refunded over the window. Never netted off the total.</summary>
    public required MoneyResponse Refunded { get; init; }

    public required int DonationCount { get; init; }

    public required int RefundedCount { get; init; }

    public required int FailedCount { get; init; }

    public required int SupporterCount { get; init; }
}

// ── GAP-008 · Recurring donations ────────────────────────────────────────────────────────────────

public sealed record RecurringDonationResponse
{
    public required Guid RecurringDonationId { get; init; }

    public required string CauseCode { get; init; }

    /// <summary>The cause's title at the time of reading, so the list needs no second call.</summary>
    public string? CauseTitle { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required int IntervalMonths { get; init; }

    public required string Status { get; init; }

    /// <summary>Null once the schedule is terminal. Never a date nothing intends to keep.</summary>
    public DateTimeOffset? NextChargeAt { get; init; }

    public DateTimeOffset? LastChargeAt { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>The reason the last attempt failed, when one did. Cleared by a success.</summary>
    public string? LastFailureReason { get; init; }

    public required int ConsecutiveFailures { get; init; }

    public required int ChargeCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }
}
