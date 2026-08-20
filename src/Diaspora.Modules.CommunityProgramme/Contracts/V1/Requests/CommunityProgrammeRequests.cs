namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What a client sends.
//
// Every write that could be double-submitted carries an optional IdempotencyKey, and the endpoint
// prefers the `Idempotency-Key` header when one is present. Both, because the register asks for
// "an Idempotency-Key header (or body field)" and the client already sends the body field on every
// Phase 12 mutation — accepting only the header would have meant a client release before a single
// application could be submitted.
//
// Nothing here carries a price the server acts on, a status the server trusts, or a capability the
// server believes. A tier's price is set by an administrator through the programme write and is
// validated against B15's currency catalogue; a membership's status is the server's; a caller's
// capabilities are read from B12's role edge on every request and are never sent.
// ════════════════════════════════════════════════════════════════════════════════════════════════

// ── Membership programme — GAP-001 ──────────────────────────────────────────────────────────────

/// <summary>One benefit on a tier.</summary>
public sealed record MembershipBenefitRequest
{
    public Guid? Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public bool IsActive { get; init; } = true;
}

/// <summary>One requirement on a tier.</summary>
public sealed record MembershipRequirementRequest
{
    public Guid? Id { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    /// <summary><c>Verification</c>, <c>Residency</c>, <c>Age</c>, <c>Invitation</c>, <c>Referral</c>, <c>Document</c>, <c>Other</c>.</summary>
    public required string Kind { get; init; }

    public string? ActionRoute { get; init; }

    /// <summary>Set on an <c>Age</c> requirement.</summary>
    public int? MinimumAge { get; init; }
}

/// <summary>One tier on a programme.</summary>
public sealed record MembershipTierRequest
{
    /// <summary>
    /// Supplied on an edit, absent on a create.
    /// <para>
    /// A tier that keeps its identifier keeps its members: the programme write replaces the tier list
    /// wholesale, and matching on this is what stops a re-posted form emptying every tier.
    /// </para>
    /// </summary>
    public Guid? TierId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Absent for a free tier. Never a zero.</summary>
    public decimal? PriceAmount { get; init; }

    /// <summary>Required when <see cref="PriceAmount"/> is present. Validated against B15's catalogue.</summary>
    public string? Currency { get; init; }

    /// <summary><c>OneOff</c>, <c>Monthly</c>, <c>Quarterly</c>, <c>Yearly</c>, <c>Lifetime</c>.</summary>
    public required string BillingPeriod { get; init; }

    public int? Capacity { get; init; }

    public bool IsRecommended { get; init; }

    public int SortOrder { get; init; }

    public IReadOnlyList<MembershipBenefitRequest>? Benefits { get; init; }

    public IReadOnlyList<MembershipRequirementRequest>? Requirements { get; init; }
}

/// <summary>
/// Opens or rewrites an organization's membership programme.
/// <para>
/// Not in the register's proposed contract, and added because a read-only surface with no way to
/// create the data behind it is not a production capability. The register proposes three reads and
/// no write; this is the write they read from.
/// </para>
/// </summary>
public sealed record SaveMembershipProgrammeRequest
{
    public required string Summary { get; init; }

    /// <summary><c>Public</c>, <c>Unlisted</c>, <c>Private</c>, <c>InviteOnly</c>.</summary>
    public required string Visibility { get; init; }

    public bool IsOpen { get; init; } = true;

    public bool RequiresApproval { get; init; } = true;

    public required IReadOnlyList<MembershipTierRequest> Tiers { get; init; }
}

// ── Membership lifecycle — GAP-002 ──────────────────────────────────────────────────────────────

/// <summary>Applies for a membership.</summary>
public sealed record SubmitMembershipApplicationRequest
{
    public required Guid TierId { get; init; }

    /// <summary>Free-form answers to the programme's questions.</summary>
    public IReadOnlyDictionary<string, string>? Answers { get; init; }

    /// <summary>§52. Persisted across retries by the client; uniquely indexed here.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Cancels the caller's own membership.</summary>
public sealed record CancelMembershipRequest
{
    public string? Reason { get; init; }
}

/// <summary>Renews the caller's own membership.</summary>
public sealed record RenewMembershipRequest
{
    public string? IdempotencyKey { get; init; }
}

/// <summary>An organization's decision about a member.</summary>
public sealed record DecideMembershipRequest
{
    /// <summary>
    /// <c>approve</c>, <c>reject</c>, <c>suspend</c>, <c>reinstate</c> or <c>remove</c>.
    /// <para>
    /// <b><c>waitlist</c> is not among them, and the omission is deliberate.</b> The register lists
    /// it, but a membership has no waiting list: a tier that is full refuses the application at
    /// submission rather than accepting it into a queue, because a place in a paid tier that frees up
    /// is not automatically the next applicant's. A volunteer opportunity does waitlist, and that is
    /// where the state lives.
    /// </para>
    /// </summary>
    public required string Decision { get; init; }

    /// <summary>Surfaced to the member verbatim.</summary>
    public string? Reason { get; init; }
}

// ── Organization roles — GAP-013 ────────────────────────────────────────────────────────────────

/// <summary>Assigns an organization role. Dispatched to B12, which owns the role edge.</summary>
public sealed record AssignOrganizationRoleRequest
{
    /// <summary><c>Member</c>, <c>Volunteer</c>, <c>Moderator</c>, <c>Organizer</c>, <c>Administrator</c>, <c>Owner</c>.</summary>
    public required string Role { get; init; }
}

// ── Volunteering — GAP-009/010/011 ──────────────────────────────────────────────────────────────

/// <summary>One shift on an opportunity.</summary>
public sealed record VolunteerShiftRequest
{
    /// <summary>Supplied on an edit; a shift that keeps its identifier keeps its assignments.</summary>
    public Guid? ShiftId { get; init; }

    public string? Label { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required int Capacity { get; init; }
}

/// <summary>Creates or rewrites a volunteer opportunity.</summary>
public sealed record SaveVolunteerOpportunityRequest
{
    public required Guid OrganizationId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Cause { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? AreaName { get; init; }

    public string? AddressLine { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public DateTimeOffset? ApplicationDeadline { get; init; }

    public int? MinimumAge { get; init; }

    public int? Capacity { get; init; }

    public IReadOnlyList<string>? RequiredSkills { get; init; }

    public IReadOnlyList<VolunteerShiftRequest>? Shifts { get; init; }

    /// <summary>Whether to publish immediately. False leaves it a draft only managers can see.</summary>
    public bool Publish { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Offers to do a piece of volunteer work.</summary>
public sealed record SubmitVolunteerApplicationRequest
{
    public Guid? ShiftId { get; init; }

    public IReadOnlyDictionary<string, string>? Answers { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>An organizer's decision about an applicant.</summary>
public sealed record DecideVolunteerApplicationRequest
{
    /// <summary><c>approve</c>, <c>reject</c> or <c>waitlist</c>.</summary>
    public required string Decision { get; init; }

    public string? Reason { get; init; }
}

/// <summary>Places an approved volunteer on a shift.</summary>
public sealed record AssignVolunteerRequest
{
    public required Guid ApplicationId { get; init; }

    /// <summary>Which shift. Required when the opportunity has any.</summary>
    public Guid? ShiftId { get; init; }
}

/// <summary>An organizer confirms what was actually done.</summary>
public sealed record ConfirmVolunteerHoursRequest
{
    public required decimal Hours { get; init; }
}

// ── Polls — GAP-012 ─────────────────────────────────────────────────────────────────────────────

/// <summary>One option on a poll.</summary>
public sealed record PollOptionRequest
{
    public required string Label { get; init; }

    public string? Description { get; init; }
}

/// <summary>Creates a poll.</summary>
public sealed record CreatePollRequest
{
    public required string Question { get; init; }

    public string? Description { get; init; }

    /// <summary><c>SingleChoice</c>, <c>MultipleChoice</c>, <c>OpenResponse</c>.</summary>
    public required string VotingType { get; init; }

    /// <summary><c>Public</c>, <c>OrganizationMembers</c>, <c>Neighborhood</c>, <c>Country</c>, <c>Invited</c>.</summary>
    public required string Audience { get; init; }

    public IReadOnlyList<PollOptionRequest>? Options { get; init; }

    public int? MaxSelections { get; init; }

    public Guid? OrganizationId { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public string? CountryCode { get; init; }

    public IReadOnlyList<Guid>? InviteeUserIds { get; init; }

    public bool IsAnonymous { get; init; }

    public bool AllowsChange { get; init; }

    /// <summary>Whether the numbers are visible before the poll closes.</summary>
    public bool ShowResultsBeforeClose { get; init; }

    public DateTimeOffset? OpensAt { get; init; }

    public DateTimeOffset? ClosesAt { get; init; }

    public bool Publish { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Casts or changes a vote.</summary>
public sealed record SubmitPollResponseRequest
{
    public IReadOnlyList<Guid>? SelectedOptionIds { get; init; }

    public string? OpenResponse { get; init; }

    public string? IdempotencyKey { get; init; }
}

// ── Announcements — GAP-016 ─────────────────────────────────────────────────────────────────────

/// <summary>Publishes a scoped announcement.</summary>
public sealed record PublishAnnouncementRequest
{
    /// <summary><c>Community</c>, <c>Organization</c>, <c>Neighborhood</c>, <c>Event</c>, <c>Campaign</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>What the scope names. Required on every scope but <c>Community</c>.</summary>
    public Guid? ScopeId { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>B5 asset identifiers, never URLs.</summary>
    public IReadOnlyList<Guid>? MediaIds { get; init; }

    public string? CountryCode { get; init; }

    public bool IsPinned { get; init; }

    public string? IdempotencyKey { get; init; }
}

// ── Membership payment binding — GAP-003 ────────────────────────────────────────────────────────

/// <summary>
/// Binds a settled B15 payment to a membership.
/// <para>
/// <b>One field, and deliberately.</b> The amount, the currency, the payer and whether the money
/// actually arrived are all read back from B15's own record of the intent. A request that carried
/// any of them would be a request a client could use to activate a membership it had not paid for,
/// which is exactly what §18 forbids the client concluding.
/// </para>
/// </summary>
public sealed record BindMembershipPaymentRequest
{
    public required Guid PaymentIntentId { get; init; }
}
