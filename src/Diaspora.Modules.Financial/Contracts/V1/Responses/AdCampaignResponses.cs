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

    /// <summary>Added 14 Oct 2026. Towns by name.</summary>
    public IReadOnlyList<string> TownNames { get; init; } = [];

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

    public string? CallToActionLabel { get; init; }

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

/// <summary>
/// The selectable values for one dimension.
/// <para>
/// <b>An empty list and no catalogue are different answers</b>, and the client renders them
/// differently: one is "pick from this empty list", the other is "type what you want, Diaspora
/// cannot enumerate these". Country is deliberately the second — it is an ISO list the app already
/// holds in full, and a second copy served from here would be a second list to keep in step.
/// </para>
/// </summary>
public sealed record AdTargetingOptionsResponse
{
    /// <summary>The dimension these belong to, as its name.</summary>
    public required string Dimension { get; init; }

    public IReadOnlyList<AdTargetingOptionResponse> Options { get; init; } = [];

    /// <summary>False when the platform cannot enumerate this dimension at all.</summary>
    public required bool HasCatalogue { get; init; }

    /// <summary>What to do instead, when there is no catalogue. Rendered verbatim.</summary>
    public string? Note { get; init; }
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

    /// <summary>
    /// Whether anything actually counted, as opposed to reporting zero because it could not.
    /// <para>
    /// <b>Without this the bounds are ambiguous and a client would guess.</b> Zero and zero means
    /// two entirely different things: "this selection reaches nobody, do not run it" and "this
    /// selection is narrowed and Diaspora cannot say by how much". Only the first is a reason to
    /// stop somebody submitting.
    /// </para>
    /// </summary>
    public required bool IsCounted { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Performance — MON-GAP-002.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What each number means, sent with every performance response.
/// <para>
/// <b>MON-GAP-002 asks for "a documented definition for every metric" and this is where it goes.</b>
/// A definition that lives in a code comment is one the person reading the dashboard never sees,
/// and §20 forbids inventing metric semantics — which includes letting a client invent them by
/// guessing what a number means.
/// </para>
/// <para>
/// Written for an advertiser, not for a reviewer of this file. They are printable as they stand.
/// </para>
/// </summary>
public sealed record AdMetricDefinitionsResponse
{
    public required string Impression { get; init; }

    public required string Click { get; init; }

    public required string Conversion { get; init; }

    public required string Result { get; init; }

    /// <summary>What is collapsed, and what is not. Currently nothing is.</summary>
    public required string Deduplication { get; init; }

    /// <summary>How far behind delivery these figures run.</summary>
    public required string ReportingLag { get; init; }
}

/// <summary>One day of delivery.</summary>
public sealed record AdPerformanceDayResponse
{
    public required DateOnly Day { get; init; }

    public required long Impressions { get; init; }

    public required long Clicks { get; init; }

    public required long Conversions { get; init; }

    public required long SpendMinorUnits { get; init; }
}

/// <summary>Delivery on one surface, so an advertiser can see where it is working.</summary>
public sealed record AdPerformanceSurfaceResponse
{
    public required string Surface { get; init; }

    public required long Impressions { get; init; }

    public required long Clicks { get; init; }
}

/// <summary>
/// How a campaign, a placement, or everything an advertiser runs is doing.
/// <para>
/// <b>Every derived figure is computed on the server.</b> Cost per result is a division that has to
/// agree with what the campaign was actually charged; a client doing it from two rounded numbers
/// would show a figure the platform never calculated.
/// </para>
/// <para>
/// <b>An absent rate is null, never zero.</b> "0% clicked" and "we have not shown this to anybody
/// yet" are different sentences, and only one of them is a reason to change the creative.
/// </para>
/// </summary>
public sealed record AdPerformanceResponse
{
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required long Impressions { get; init; }

    public required long Clicks { get; init; }

    public required long Conversions { get; init; }

    /// <summary>Clicks over impressions, six decimal places. Null when nothing was served.</summary>
    public decimal? ClickThroughRate { get; init; }

    public required AdMoneyResponse Spend { get; init; }

    /// <summary>What the objective counts. See <see cref="AdMetricDefinitionsResponse.Result"/>.</summary>
    public required long Results { get; init; }

    /// <summary>Spend over results. Null when there are none — dividing by zero is not "free".</summary>
    public AdMoneyResponse? CostPerResult { get; init; }

    /// <summary>
    /// How much of the budget has gone, 0 to 1. Null for a placement or a rollup, which have no
    /// single budget behind them.
    /// </summary>
    public decimal? BudgetConsumedFraction { get; init; }

    public IReadOnlyList<AdPerformanceDayResponse> Daily { get; init; } = [];

    public IReadOnlyList<AdPerformanceSurfaceResponse> BySurface { get; init; } = [];

    public required AdMetricDefinitionsResponse Definitions { get; init; }
}

/// <summary>
/// What a submission answers — MON-GAP-001.
/// <para>
/// <b>The campaign, and everything needed to pay for it in one call.</b> The register asked for
/// "the payment intent to fund it"; the intent is opened by the existing purchase route instead,
/// because a handler that dispatched into that route is what DSP1001 forbids. What comes back here
/// is the exact triple that route needs — package, subject type, subject id — so the client makes
/// one more call and never has to work out what to buy.
/// </para>
/// <para>
/// <b>The campaign is already frozen when this returns.</b> Editing one somebody is halfway through
/// paying for is what that ordering prevents.
/// </para>
/// </summary>
public sealed record AdCampaignSubmissionResponse
{
    public required AdCampaignResponse Campaign { get; init; }

    /// <summary>The package to buy. Pass it to the existing purchase route.</summary>
    public required string ProductCode { get; init; }

    /// <summary>Always <c>Campaign</c>. What the purchase is for.</summary>
    public required string SubjectType { get; init; }

    /// <summary>The campaign's own id. The capture uses it to find its way back.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>What the package costs, which is the campaign's budget.</summary>
    public required AdMoneyResponse Amount { get; init; }
}
