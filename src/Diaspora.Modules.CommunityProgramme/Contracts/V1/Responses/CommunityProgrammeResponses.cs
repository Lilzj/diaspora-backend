namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The module's wire contracts.
//
// Shaped to the frontend's Phase 12 domain model, because the whole point of closing a gap is that
// the client's gap prefix changes to this module's and nothing else moves. Where the register named
// a field, it is here under that name; where it did not, the name follows the platform's own
// conventions.
//
// Three properties hold throughout, and each of them is a decision the register asks the server to
// make so the client cannot:
//
//   Every "can I" is a field. isFull, canApply, canVote, canManage, isMet, isRenewable. The client
//   renders the answer and computes none of them, because each depends on facts a device does not
//   have — the caller's tier, the other applicants, the account's standing.
//
//   Every refusal carries its reason as text. cannotApplyReason and cannotVoteReason are printed
//   verbatim; a client composing its own explanation would be describing a decision it did not make.
//
//   An absence is never a zero. A free tier has no price rather than a zero one, and a poll
//   withholding its results returns null options rather than an empty array — an empty array renders
//   as "nobody voted", which is a different and false statement.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>An amount and the currency it is denominated in. B15's shape, never converted.</summary>
public sealed record MoneyResponse
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>How many minor units make one major one. Two for GBP, zero for JPY.</summary>
    public required int Exponent { get; init; }
}

/// <summary>The cursor-paged envelope every list in this module uses.</summary>
public sealed record CommunityPageResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>Absent where the module cannot count without scanning. Never guessed.</summary>
    public int? TotalCount { get; init; }
}

// ── Organizations and capabilities — GAP-013 ────────────────────────────────────────────────────

/// <summary>
/// What the caller may do inside one organization.
/// <para>
/// Ten flags and a label. The label is B12's role name and is for display; the flags are the
/// contract, and every gated route re-decides them rather than trusting anything the client sends.
/// </para>
/// </summary>
public sealed record OrganizationCapabilitiesResponse
{
    public required Guid OrganizationId { get; init; }

    /// <summary>Null when the caller holds no role in this organization.</summary>
    public string? Role { get; init; }

    public required bool CanManageMembers { get; init; }

    public required bool CanApproveMembers { get; init; }

    public required bool CanManageTiers { get; init; }

    public required bool CanManageEvents { get; init; }

    public required bool CanManageCampaigns { get; init; }

    public required bool CanManageVolunteers { get; init; }

    public required bool CanManagePolls { get; init; }

    public required bool CanPublishAnnouncements { get; init; }

    public required bool CanViewAnalytics { get; init; }

    public required bool CanViewFinancials { get; init; }
}

/// <summary>One of the caller's organizations, with the role and the capability set.</summary>
public sealed record MyOrganizationResponse
{
    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    /// <summary>A B5 asset identifier, never a URL. The client asks Media for the rendition.</summary>
    public Guid? LogoMediaId { get; init; }

    public required string Role { get; init; }

    /// <summary>B12's membership row. What the role-change route names.</summary>
    public required Guid MembershipId { get; init; }

    public required bool IsVerified { get; init; }

    public required OrganizationCapabilitiesResponse Capabilities { get; init; }
}

/// <summary>
/// The organization dashboard's first paint.
/// <para>
/// One call, and the counts are the point: they let a section be skipped rather than fetched to
/// discover it is empty. On an organization that only runs events, that is the difference between
/// one request and nine.
/// </para>
/// </summary>
public sealed record OrganizationDashboardResponse
{
    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public Guid? LogoMediaId { get; init; }

    public required OrganizationCapabilitiesResponse Capabilities { get; init; }

    public required DashboardCountsResponse Counts { get; init; }

    /// <summary>When these numbers were computed. Every one of them is read live.</summary>
    public required DateTimeOffset ComputedAt { get; init; }
}

/// <summary>The dashboard's section counts.</summary>
public sealed record DashboardCountsResponse
{
    public required int PendingMemberApplications { get; init; }

    public required int ActiveMembers { get; init; }

    public required int OpenOpportunities { get; init; }

    public required int PendingVolunteerApplications { get; init; }

    public required int OpenPolls { get; init; }

    public required int RecentAnnouncements { get; init; }
}

// ── Membership programme — GAP-001 ──────────────────────────────────────────────────────────────

/// <summary>Something a tier gets you.</summary>
public sealed record MembershipBenefitResponse
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>An icon name the server chose. The catalogue owns its own iconography.</summary>
    public string? Icon { get; init; }

    public required bool IsActive { get; init; }
}

/// <summary>
/// Something a tier asks of an applicant, and whether the caller meets it.
/// <para>
/// <see cref="IsMet"/> is the server's evaluation, made on every read. §19 forbids the client
/// computing one, and it could not: eligibility depends on the account's standing, which the device
/// does not hold.
/// </para>
/// </summary>
public sealed record MembershipRequirementResponse
{
    public required Guid Id { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public required string Kind { get; init; }

    public required bool IsMet { get; init; }

    /// <summary>Where to send somebody who does not meet it yet.</summary>
    public string? ActionRoute { get; init; }
}

/// <summary>One rung of a membership programme.</summary>
public sealed record MembershipTierResponse
{
    public required Guid TierId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Null for a free tier. Never a zero — the absence is the meaning.</summary>
    public MoneyResponse? Price { get; init; }

    public required string BillingPeriod { get; init; }

    public required IReadOnlyList<MembershipBenefitResponse> Benefits { get; init; }

    public required IReadOnlyList<MembershipRequirementResponse> Requirements { get; init; }

    /// <summary>Null where the tier is uncapped.</summary>
    public int? Capacity { get; init; }

    public required int FilledCount { get; init; }

    /// <summary>Server-decided. The client does not compare the two above.</summary>
    public required bool IsFull { get; init; }

    public required bool IsRecommended { get; init; }

    public required int SortOrder { get; init; }
}

/// <summary>An organization's membership programme, with the caller's own standing against it.</summary>
public sealed record MembershipProgrammeResponse
{
    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public Guid? OrganizationLogoMediaId { get; init; }

    public required string Category { get; init; }

    public required bool IsVerified { get; init; }

    public required string Visibility { get; init; }

    public required bool IsOpen { get; init; }

    public required bool RequiresApproval { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<MembershipTierResponse> Tiers { get; init; }

    public required int MemberCount { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>The caller's own state. One of the fourteen, including the three never stored.</summary>
    public required string MyStatus { get; init; }

    public Guid? MyMembershipId { get; init; }
}

// ── Membership lifecycle — GAP-002 ──────────────────────────────────────────────────────────────

/// <summary>One step in a membership's life, as the member sees it.</summary>
public sealed record MembershipHistoryEntryResponse
{
    public required Guid Id { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// <c>Member</c>, <c>Organization</c> or <c>System</c>.
    /// <para>
    /// A category rather than a person: the member is entitled to know the organization refused
    /// them, and is not entitled to know which administrator clicked the button.
    /// </para>
    /// </summary>
    public required string Actor { get; init; }

    public string? Reason { get; init; }
}

/// <summary>The caller's own membership of one organization's programme.</summary>
public sealed record ProgrammeMembershipResponse
{
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public Guid? OrganizationLogoMediaId { get; init; }

    public required Guid TierId { get; init; }

    public required string TierName { get; init; }

    public required string Status { get; init; }

    public MoneyResponse? Price { get; init; }

    public required string BillingPeriod { get; init; }

    public required IReadOnlyList<MembershipBenefitResponse> Benefits { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? RenewalDueAt { get; init; }

    /// <summary>Server-decided. The client does not compare dates to reach it.</summary>
    public required bool IsRenewable { get; init; }

    /// <summary>Whether the membership currently entitles its holder to the tier's benefits.</summary>
    public required bool GrantsBenefits { get; init; }

    /// <summary>The B15 payment intent behind it, when there is one. An identifier and nothing else.</summary>
    public Guid? PaymentIntentId { get; init; }

    public required IReadOnlyList<MembershipHistoryEntryResponse> History { get; init; }

    public string? DecisionReason { get; init; }
}

/// <summary>
/// A member as their organization's roll sees them.
/// <para>
/// <b>Display name, tier, status, role and dates.</b> No email, no telephone, no address, no payment
/// details — §45, and the reason the roll is a different contract from the member's own read.
/// </para>
/// </summary>
public sealed record OrganizationMemberResponse
{
    public required Guid MembershipId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>
    /// The member's name, or null when the platform cannot say.
    /// <para>
    /// Resolved once per page through <c>IMemberProfileService</c>, the sanctioned batch source,
    /// and never invented. Null is a real answer and the client renders a neutral label for it —
    /// the identifier is not a name, and showing one asks an administrator to decide about
    /// "3f2a…" as though that were a person.
    /// </para>
    /// </summary>
    public string? DisplayName { get; init; }

    public required Guid TierId { get; init; }

    public required string TierName { get; init; }

    public required string Status { get; init; }

    /// <summary>B12's role, when the member also holds one. Null when they hold none.</summary>
    public string? Role { get; init; }

    /// <summary>B12's membership row, when the member holds a role. What the role route names.</summary>
    public Guid? OrganizationMembershipId { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }
}

// ── Volunteering — GAP-009/010/011 ──────────────────────────────────────────────────────────────

/// <summary>One dated slot on an opportunity.</summary>
public sealed record VolunteerShiftResponse
{
    public required Guid ShiftId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required string Label { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required int Capacity { get; init; }

    public required int FilledCount { get; init; }

    /// <summary>Server-decided.</summary>
    public required bool IsFull { get; init; }
}

/// <summary>A piece of volunteer work, and the server's decision about whether the caller may take it.</summary>
public sealed record VolunteerOpportunityResponse
{
    public required Guid OpportunityId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Cause { get; init; }

    public required string Status { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public Guid? OrganizationLogoMediaId { get; init; }

    public required bool IsVerified { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? AreaName { get; init; }

    public string? AddressLine { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public required IReadOnlyList<VolunteerShiftResponse> Shifts { get; init; }

    public required IReadOnlyList<string> RequiredSkills { get; init; }

    public int? MinimumAge { get; init; }

    public int? Capacity { get; init; }

    public required int FilledCount { get; init; }

    public required int WaitlistCount { get; init; }

    /// <summary>Server-decided. An uncapped opportunity is never full.</summary>
    public required bool IsFull { get; init; }

    public DateTimeOffset? ApplicationDeadline { get; init; }

    /// <summary>The caller's own state. <c>NotApplied</c> when they hold no application.</summary>
    public required string MyApplicationStatus { get; init; }

    public Guid? MyApplicationId { get; init; }

    /// <summary>Whether the server will accept an application from this caller.</summary>
    public required bool CanApply { get; init; }

    /// <summary>Why not, when it will not. Rendered verbatim; the client composes nothing.</summary>
    public string? CannotApplyReason { get; init; }

    public required bool CanManage { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>The caller's own volunteer application.</summary>
public sealed record VolunteerApplicationResponse
{
    public required Guid ApplicationId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required string OpportunityTitle { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public Guid? ShiftId { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyDictionary<string, string> Answers { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionReason { get; init; }

    /// <summary>
    /// Where in the queue, when waitlisted.
    /// <para>
    /// Computed at read time from the other waitlisted applications, because a stored position goes
    /// wrong the moment somebody ahead withdraws.
    /// </para>
    /// </summary>
    public int? WaitlistPosition { get; init; }

    public VolunteerAssignmentResponse? Assignment { get; init; }
}

/// <summary>
/// An applicant as the organizer's queue sees them.
/// <para>
/// Display name and their own answers. No contact details — §45. An organizer who needs to reach
/// somebody uses B13.2 conversations, and this module adds no messaging.
/// </para>
/// </summary>
public sealed record VolunteerApplicantResponse
{
    public required Guid ApplicationId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>
    /// The member's name, or null when the platform cannot say.
    /// <para>
    /// Resolved once per page through <c>IMemberProfileService</c>, the sanctioned batch source,
    /// and never invented. Null is a real answer and the client renders a neutral label for it —
    /// the identifier is not a name, and showing one asks an administrator to decide about
    /// "3f2a…" as though that were a person.
    /// </para>
    /// </summary>
    public string? DisplayName { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyDictionary<string, string> Answers { get; init; }

    public Guid? ShiftId { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    public int? WaitlistPosition { get; init; }
}

/// <summary>An approved volunteer's placement, and the hours somebody stood behind.</summary>
public sealed record VolunteerAssignmentResponse
{
    public required Guid AssignmentId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required string OpportunityTitle { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid VolunteerUserId { get; init; }

    public Guid? ShiftId { get; init; }

    public string? ShiftLabel { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }

    public DateTimeOffset? CheckedOutAt { get; init; }

    /// <summary>Confirmed by an organizer. Null until then — never self-reported.</summary>
    public decimal? ConfirmedHours { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }
}

/// <summary>An issued record of confirmed volunteering.</summary>
public sealed record VolunteerCertificateResponse
{
    public required Guid CertificateId { get; init; }

    public required string Title { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public required decimal Hours { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// Absent, and honestly so.
    /// <para>
    /// Rendering a signed document is a service this platform does not have. The certificate is real,
    /// listed and traceable to the organization that confirmed it; the file is what a later phase
    /// adds, and this field stays null rather than pointing at something that 404s.
    /// </para>
    /// </summary>
    public string? DownloadUrl { get; init; }
}

/// <summary>The caller's volunteering record. Confirmed hours only.</summary>
public sealed record VolunteerHoursResponse
{
    /// <summary>Organizer-confirmed only. §34: a self-reported hour is not an hour.</summary>
    public required decimal ConfirmedHours { get; init; }

    public required int CompletedAssignments { get; init; }

    public required int OrganizationsSupported { get; init; }

    public required IReadOnlyList<VolunteerCertificateResponse> Certificates { get; init; }
}

// ── Polls — GAP-012 ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One answer a poll offers.
/// <para>
/// <b>No count.</b> A component holding a poll must have nothing to add up — which is what makes
/// server-computed results structurally true rather than a convention somebody could break.
/// </para>
/// </summary>
public sealed record PollOptionResponse
{
    public required Guid OptionId { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public required int SortOrder { get; init; }
}

/// <summary>A question put to a community, with the caller's own standing against it.</summary>
public sealed record CommunityPollResponse
{
    public required Guid PollId { get; init; }

    public required string Question { get; init; }

    public string? Description { get; init; }

    public required string Status { get; init; }

    public required string VotingType { get; init; }

    public required string Audience { get; init; }

    /// <summary>Added 14 Oct 2026. The country a country or neighbourhood poll is about.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Added 14 Oct 2026. The town a neighbourhood poll is about.</summary>
    public string? CityName { get; init; }

    public required IReadOnlyList<PollOptionResponse> Options { get; init; }

    public int? MaxSelections { get; init; }

    public required bool IsAnonymous { get; init; }

    public required bool AllowsChange { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? OrganizationName { get; init; }

    public DateTimeOffset? OpensAt { get; init; }

    public DateTimeOffset? ClosesAt { get; init; }

    /// <summary>How many have answered. A total, and deliberately not a breakdown.</summary>
    public required int TotalResponses { get; init; }

    public required bool HasVoted { get; init; }

    /// <summary>The caller's own answer. Absent on an anonymous poll, structurally.</summary>
    public PollResponseDetail? MyResponse { get; init; }

    /// <summary>Server-decided.</summary>
    public required bool CanVote { get; init; }

    /// <summary>Why not, when they cannot. Rendered verbatim.</summary>
    public string? CannotVoteReason { get; init; }

    public required bool CanManage { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>The caller's own answer.</summary>
public sealed record PollResponseDetail
{
    public required Guid ResponseId { get; init; }

    public required Guid PollId { get; init; }

    public required IReadOnlyList<Guid> SelectedOptionIds { get; init; }

    public string? OpenResponse { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }
}

/// <summary>One option's share of the vote. Server-computed, both fields.</summary>
public sealed record PollOptionResultResponse
{
    public required Guid OptionId { get; init; }

    public required string Label { get; init; }

    public required int Count { get; init; }

    /// <summary>0–100, rounded to one place. Never derived on the device from the two above.</summary>
    public required decimal Percentage { get; init; }

    /// <summary>True for the option the caller chose. Absent on an anonymous poll.</summary>
    public bool? IsMyChoice { get; init; }
}

/// <summary>
/// The tally.
/// <para>
/// <b>A poll withholding its results returns <see cref="Options"/> null, not an empty array.</b> An
/// empty array renders as "nobody voted", which is a different and false statement — and the client
/// draws <see cref="ResultsHiddenReason"/> in its place.
/// </para>
/// </summary>
public sealed record PollResultResponse
{
    public required Guid PollId { get; init; }

    public required string Status { get; init; }

    /// <summary>Null while results are withheld. Never an empty array in that case.</summary>
    public IReadOnlyList<PollOptionResultResponse>? Options { get; init; }

    public required int TotalResponses { get; init; }

    /// <summary>Free-text answers on an open-response poll, when the caller may see them.</summary>
    public IReadOnlyList<string>? OpenResponses { get; init; }

    public required bool AreResultsVisible { get; init; }

    public string? ResultsHiddenReason { get; init; }

    public required DateTimeOffset ComputedAt { get; init; }
}

// ── Announcements — GAP-016 ─────────────────────────────────────────────────────────────────────

/// <summary>A published post, as the feed renders it.</summary>
public sealed record CommunityAnnouncementResponse
{
    public required Guid AnnouncementId { get; init; }

    public required string Scope { get; init; }

    public Guid? ScopeId { get; init; }

    /// <summary>What the scope is called. Resolved from whichever module owns it.</summary>
    public required string ScopeName { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>B5 asset identifiers, never URLs.</summary>
    public required IReadOnlyList<Guid> MediaIds { get; init; }

    public required Guid PublishedByUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required bool IsPinned { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The route the platform inbox deep-links to.</summary>
    public string? ActionRoute { get; init; }
}

// ── Analytics and impact — GAP-014/017 ──────────────────────────────────────────────────────────

/// <summary>One point on a time series.</summary>
public sealed record TimeSeriesPointResponse
{
    /// <summary>The day, as YYYY-MM-DD.</summary>
    public required string Date { get; init; }

    public required decimal Value { get; init; }
}

/// <summary>
/// An organization's programme analytics.
/// <para>
/// <b>Computed on read, from the rows themselves.</b> There is no projection table and no
/// recomputation cadence to be stale against: <see cref="ComputedAt"/> is the moment the request was
/// served, and every figure is as fresh as the database. That is a deliberate trade — the alternative
/// is a nightly rollup that is a day old and a second place for a number to be wrong.
/// </para>
/// </summary>
public sealed record OrganizationAnalyticsResponse
{
    public required Guid OrganizationId { get; init; }

    public required string Period { get; init; }

    public required IReadOnlyList<TimeSeriesPointResponse> MemberGrowth { get; init; }

    public required IReadOnlyList<TimeSeriesPointResponse> VolunteerHours { get; init; }

    public required IReadOnlyList<TimeSeriesPointResponse> PollParticipation { get; init; }

    /// <summary>When these numbers were computed. Here, the moment they were asked for.</summary>
    public required DateTimeOffset ComputedAt { get; init; }
}

/// <summary>An organization's membership analytics, broken down by state and tier.</summary>
public sealed record MemberAnalyticsResponse
{
    public required Guid OrganizationId { get; init; }

    public required string Period { get; init; }

    public required int TotalMembers { get; init; }

    public required int ActiveMembers { get; init; }

    public required int PendingApplications { get; init; }

    public required int LapsedMembers { get; init; }

    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }

    public required IReadOnlyList<TimeSeriesPointResponse> Joins { get; init; }

    public required DateTimeOffset ComputedAt { get; init; }
}

/// <summary>
/// A figure and its definition.
/// <para>
/// <b>The definition travels with the number, and that is the register's requirement rather than a
/// nicety.</b> "An undefined metric is worse than an absent one": a member reading "12 events
/// attended" has to be able to find out what counted as attending, and a definition in a wiki
/// somewhere is a definition nobody reads.
/// </para>
/// </summary>
public sealed record ImpactMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required decimal Value { get; init; }

    /// <summary>What was counted, in one sentence. Printed beside the figure.</summary>
    public required string Definition { get; init; }

    /// <summary>Which module the figure came from, so it can be reconciled.</summary>
    public required string Source { get; init; }
}

/// <summary>Community impact over a scope and a period.</summary>
public sealed record CommunityImpactResponse
{
    public required string Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public required string ScopeName { get; init; }

    public required string Period { get; init; }

    public required IReadOnlyList<ImpactMetricResponse> Metrics { get; init; }

    public required DateTimeOffset ComputedAt { get; init; }
}

// ── Search — GAP-021 ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row of the federated Community Life search.
/// <para>
/// <b>The order is the server's decision.</b> The register's complaint was that the client had to
/// concatenate two result sets and label them by source; this route returns one ordered list, and
/// <see cref="Relevance"/> is what ordered it.
/// </para>
/// </summary>
public sealed record CommunitySearchResultResponse
{
    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public Guid? ImageMediaId { get; init; }

    public string? LocationLabel { get; init; }

    public string? Badge { get; init; }

    public required string ActionRoute { get; init; }

    /// <summary>The server's score. Higher is more relevant; the client re-orders nothing.</summary>
    public required double Relevance { get; init; }
}
