namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B15.1 Giving Extension — request bodies.
//
// No request here carries a status, a raised total, a supporter count or a percentage. Every one of
// those is the server's, and a field a client could send would be a field somebody eventually
// trusts.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Bind a settled B15 payment to a community membership. COMMUNITY-LIFE-GAP-003.</summary>
public sealed record BindMembershipPaymentRequest
{
    /// <summary>
    /// The payment intent that paid for it.
    /// <para>
    /// <b>The only field, and deliberately.</b> The amount, the currency, the payer and whether it
    /// settled are all read back from B15's own record. A request that carried any of them would be
    /// a request a client could use to claim a membership it had not paid for.
    /// </para>
    /// </summary>
    public required Guid PaymentIntentId { get; init; }
}

/// <summary>Create a fundraising campaign. COMMUNITY-LIFE-GAP-006.</summary>
public sealed record CreateCampaignRequest
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Story { get; init; }

    /// <summary>A Media reference, never a URL.</summary>
    public string? CoverImage { get; init; }

    /// <summary>Media references, never URLs.</summary>
    public IReadOnlyList<string>? Gallery { get; init; }

    public required string Category { get; init; }

    /// <summary>Major units, as the client already renders every price. Null for no target.</summary>
    public decimal? TargetAmount { get; init; }

    public required string Currency { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    public required string BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public required string BeneficiaryName { get; init; }

    /// <summary>
    /// The organization running it, when it is not a personal campaign.
    /// <para>
    /// Present means the caller must hold <c>canManageCampaigns</c> inside that body, asked of the
    /// module that owns the role edge. Absent means the caller's own verified standing is the bar.
    /// </para>
    /// </summary>
    public Guid? OrganizationId { get; init; }

    public string? CountryCode { get; init; }

    public string? LocationLabel { get; init; }

    public IReadOnlyList<string>? AcceptedKinds { get; init; }

    /// <summary>Refused server-side when false. Terms acceptance is not a form-level check.</summary>
    public required bool AcceptedTerms { get; init; }

    /// <summary>B15's idempotency primitive. A retried creation replays rather than duplicating.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Edit a campaign's own text. Nothing financial moves through it.</summary>
public sealed record UpdateCampaignRequest
{
    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Story { get; init; }

    public string? CoverImage { get; init; }

    public IReadOnlyList<string>? Gallery { get; init; }

    public string? Category { get; init; }

    public decimal? TargetAmount { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    public string? Urgency { get; init; }

    public string? LocationLabel { get; init; }

    public IReadOnlyList<string>? AcceptedKinds { get; init; }
}

/// <summary>A lifecycle move. The reason is carried on a rejection and a cancellation.</summary>
public sealed record CampaignTransitionRequest
{
    public string? Reason { get; init; }
}

/// <summary>Publish an update to a campaign's supporters. COMMUNITY-LIFE-GAP-007.</summary>
public sealed record CreateCampaignUpdateRequest
{
    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>Media references, never URLs.</summary>
    public IReadOnlyList<string>? MediaReferences { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Declare a campaign's milestones. Whether they are reached is never the caller's.</summary>
public sealed record SetCampaignMilestonesRequest
{
    public required IReadOnlyList<CampaignMilestoneInput> Milestones { get; init; }
}

/// <summary>One declared milestone. No <c>isReached</c> — that is the server's answer alone.</summary>
public sealed record CampaignMilestoneInput
{
    /// <summary>Omit to declare a new one; supply the existing id to keep it reached.</summary>
    public Guid? MilestoneId { get; init; }

    public required string Label { get; init; }

    /// <summary>Major units, in the campaign's own currency.</summary>
    public required decimal TargetAmount { get; init; }
}

/// <summary>Declare how a campaign's money is allocated, and report what has been spent.</summary>
public sealed record SetCampaignFundUsageRequest
{
    public required IReadOnlyList<FundUsageLineInput> Lines { get; init; }
}

/// <summary>One allocation line.</summary>
public sealed record FundUsageLineInput
{
    public Guid? Id { get; init; }

    public required string Label { get; init; }

    /// <summary>Major units, in the campaign's own currency.</summary>
    public required decimal Allocated { get; init; }

    /// <summary>Major units. Null where nothing has been spent against this line yet.</summary>
    public decimal? Spent { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// The B15 payout that backs the spend, when one does.
    /// <para>
    /// Verified against the caller's own settled withdrawals before it is stored. A reference the
    /// caller does not own is refused rather than recorded as verification.
    /// </para>
    /// </summary>
    public Guid? WithdrawalId { get; init; }
}
