namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Advertising campaigns — MON-GAP-001, -002, -006, -007.
//
// The client has drawn this whole surface against simulated persistence for a phase: dashboard,
// wizard, objective, placement, creative, audience, budget, schedule, preview, checkout,
// submission, review, pause, resume, cancel, performance. These are the shapes it already
// destructures, so closing the gap is a change of arm and not a change of screen.
//
// Two properties hold throughout:
//
//   Every "can I" is a field. canEdit, canSubmit, canPause, canResume, canCancel, canAppeal. Each
//   depends on the campaign's state and on who is asking, and the client computes none of them —
//   a dashboard that derived "pausable" from a status string would be re-implementing the state
//   machine on a device, in a language that cannot see the aggregate.
//
//   Every money figure carries its currency and exponent. A budget of 10000 is £100.00 or ¥10000
//   depending on a number that has to travel with it.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An amount and the currency it is in. B15's shape, never converted.</summary>
public sealed record AdMoneyResponse
{
    public required long MinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>How many minor units make one major one. Two for GBP, zero for JPY.</summary>
    public required int Exponent { get; init; }
}

/// <summary>
/// Who a campaign is aimed at.
/// <para>
/// <b><see cref="ConstrainedDimensions"/> is the field that answers MON-GAP-007's second half.</b>
/// The register asked for "a statement of which dimensions the delivery engine actually honours",
/// because the builder collected seven and the wire carried one. Every array here is honoured; the
/// list names which of them this campaign actually narrows on, so a client can say "narrowed by
/// country and community" without inspecting five arrays and inferring it.
/// </para>
/// </summary>
public sealed record AdAudienceResponse
{
    public IReadOnlyList<string> CountryCodes { get; init; } = [];

    public IReadOnlyList<Guid> CommunityIds { get; init; } = [];

    public IReadOnlyList<Guid> NeighbourhoodIds { get; init; } = [];

    public IReadOnlyList<string> Interests { get; init; } = [];

    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>True when the campaign narrows on nothing at all.</summary>
    public required bool IsEveryone { get; init; }

    /// <summary>The dimension names this audience constrains. See the type summary.</summary>
    public IReadOnlyList<string> ConstrainedDimensions { get; init; } = [];
}

/// <summary>One placement a campaign booked.</summary>
public sealed record AdCampaignPlacementResponse
{
    public required Guid PlacementId { get; init; }

    /// <summary>The slot kind, as its name.</summary>
    public required string Kind { get; init; }
}

/// <summary>
/// The review, as the advertiser sees it.
/// <para>
/// Present on every campaign, not only rejected ones, because "nobody has decided yet" is an answer
/// the client renders and an absent object is not. <see cref="Decision"/> is <c>Pending</c> until
/// somebody looks.
/// </para>
/// </summary>
public sealed record AdCampaignReviewResponse
{
    /// <summary><c>Pending</c>, <c>Approved</c> or <c>Rejected</c>.</summary>
    public required string Decision { get; init; }

    /// <summary>The reviewer's words, verbatim. Required on a rejection (§74).</summary>
    public string? Note { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Whether the advertiser may contest it. One appeal, not a thread.</summary>
    public required bool CanAppeal { get; init; }

    public string? AppealNote { get; init; }

    public DateTimeOffset? AppealedAt { get; init; }
}

/// <summary>
/// A campaign, in full.
/// <para>
/// <b>The reviewer's identity is not on it.</b> The decision, the note and the date are; who made it
/// is not something an advertiser needs and is something a rejected advertiser might act on.
/// </para>
/// </summary>
public sealed record AdCampaignResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the four <c>AdCampaignObjective</c> names.</summary>
    public required string Objective { get; init; }

    /// <summary>One of the twelve <c>AdCampaignStatus</c> names.</summary>
    public required string Status { get; init; }

    public required AdMoneyResponse Budget { get; init; }

    public required AdMoneyResponse Spend { get; init; }

    /// <summary>Budget minus spend. Sent rather than left as arithmetic, so one place computes it.</summary>
    public required AdMoneyResponse Remaining { get; init; }

    /// <summary>What was owed back on a cancellation. Null on every other ending.</summary>
    public AdMoneyResponse? Settled { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public required AdAudienceResponse Audience { get; init; }

    /// <summary>The creative's image, as a media id. The device resolves it.</summary>
    public Guid? CreativeMediaId { get; init; }

    public string? Headline { get; init; }

    public string? Body { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? ProductCode { get; init; }

    /// <summary>B15's purchase, once funded. Null while the campaign is a draft.</summary>
    public Guid? PurchaseId { get; init; }

    public IReadOnlyList<AdCampaignPlacementResponse> Placements { get; init; } = [];

    public required AdCampaignReviewResponse Review { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? ActivatedAt { get; init; }

    public DateTimeOffset? PausedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>Why it ended. Rendered verbatim, and distinct from a review note.</summary>
    public string? EndReason { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // ── What the caller may do, decided here ────────────────────────────────────────────────

    public required bool CanEdit { get; init; }

    public required bool CanSubmit { get; init; }

    public required bool CanPause { get; init; }

    public required bool CanResume { get; init; }

    public required bool CanCancel { get; init; }
}

/// <summary>
/// What a targeting dimension offers — MON-GAP-007.
/// <para>
/// <b>Owned by the backend rather than hard-coded in the app</b>, which is what the register asked
/// for by name. A client with its own copy of the country list is a client that disagrees with the
/// delivery engine the first time one of them changes.
/// </para>
/// </summary>
public sealed record AdTargetingOptionResponse
{
    /// <summary>What to send back in the audience.</summary>
    public required string Value { get; init; }

    /// <summary>What to show. Already localised by whoever produced it.</summary>
    public required string Label { get; init; }
}

/// <summary>The selectable values for one dimension.</summary>
public sealed record AdTargetingOptionsResponse
{
    /// <summary>The dimension these belong to, as its name.</summary>
    public required string Dimension { get; init; }

    public IReadOnlyList<AdTargetingOptionResponse> Options { get; init; } = [];
}

/// <summary>
/// How many people a selection could reach — MON-GAP-007.
/// <para>
/// <b>A range and a basis, not a number.</b> §29 forbids inventing an authoritative metric, and a
/// single figure would be exactly that: the reachable audience depends on who opens the app
/// tomorrow. The client shows the range and the sentence, and cannot show a precision the platform
/// does not have.
/// </para>
/// </summary>
public sealed record AdAudienceEstimateResponse
{
    public required long LowerBound { get; init; }

    public required long UpperBound { get; init; }

    /// <summary>Which dimensions actually narrowed this estimate.</summary>
    public IReadOnlyList<string> ConstrainedDimensions { get; init; } = [];

    /// <summary>
    /// How the figure was arrived at, in a sentence the client renders verbatim. An estimate
    /// somebody cannot explain is an estimate nobody should act on.
    /// </summary>
    public required string Basis { get; init; }

    /// <summary>
    /// False when the platform cannot narrow on the selection at all and is reporting its whole
    /// reachable population. The client must not present that as a targeted reach.
    /// </summary>
    public required bool IsNarrowed { get; init; }
}
