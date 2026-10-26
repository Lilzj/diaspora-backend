namespace Diaspora.Modules.Creator.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// What the platform serves.
//
// Every shape here mirrors the frontend's own `src/types/creator-economy.ts`, because the register's
// premise is that closing a gap changes the client's URL prefix and nothing else. Where this file
// adds a field the client does not yet read, the addition is always the same kind of thing: a
// statement about what the platform knows, so an absence is legible rather than silent.
//
// Enum values are served as their names, which is what the client's open string unions read.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>One requirement toward a status, a verification, a challenge or a grant.</summary>
public sealed record CreatorRequirementResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    /// <summary>Decided by the server. See <c>CreatorRequirement</c> for why that matters.</summary>
    public required bool IsMet { get; init; }

    public string? ActionRoute { get; init; }

    public string? ActionLabel { get; init; }

    public long? Current { get; init; }

    public long? Target { get; init; }
}

/// <summary>A progress figure the server computed. <c>percentage</c> is never a client's division.</summary>
public sealed record CreatorProgressResponse
{
    public required long Current { get; init; }

    public required long Target { get; init; }

    public required int Percentage { get; init; }

    public string? Unit { get; init; }
}

/// <summary>What the platform says the next rung needs.</summary>
public sealed record CreatorStatusTargetResponse
{
    public required string Status { get; init; }

    public required string Label { get; init; }

    public required IReadOnlyList<CreatorRequirementResponse> Requirements { get; init; }
}

/// <summary>
/// Audience figures.
/// <para>
/// <b><see cref="Reach"/> is zero and <see cref="ReachAvailable"/> is false, and that pair is the
/// honest answer rather than an omission.</b> Reach is an audience measurement — CREATOR-GAP-007 —
/// and belongs to B19; §21 of the phase brief forbids inventing it from the lifetime counters this
/// module does hold. Serving a zero keeps the client's non-nullable field working; serving the flag
/// beside it means the zero says "not measured" rather than "nobody".
/// </para>
/// </summary>
public sealed record CreatorAudienceResponse
{
    public required long Followers { get; init; }

    public required long Following { get; init; }

    public required long Reach { get; init; }

    /// <summary>Whether <see cref="Reach"/> is a measurement. False until B19 lands.</summary>
    public required bool ReachAvailable { get; init; }

    public required long TotalContentPublished { get; init; }

    public required long TotalHelpfulVotes { get; init; }
}

public sealed record CreatorCompletenessResponse
{
    public required int Percentage { get; init; }

    public required IReadOnlyList<CreatorRequirementResponse> Missing { get; init; }
}

/// <summary>A badge, earned by claiming a milestone that grants one.</summary>
public sealed record CreatorBadgeResponse
{
    public required string BadgeId { get; init; }

    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public required bool IsEarned { get; init; }

    public DateTimeOffset? EarnedAt { get; init; }
}

/// <summary>CREATOR-GAP-001. The caller's own creator projection.</summary>
public sealed record CreatorProfileResponse
{
    public required Guid CreatorId { get; init; }

    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? Handle { get; init; }

    public string? AvatarUrl { get; init; }

    public string? CoverImageUrl { get; init; }

    public string? Bio { get; init; }

    /// <summary>The platform's answer. Never derived from a score by a client.</summary>
    public required string Status { get; init; }

    public required DateTimeOffset StatusChangedAt { get; init; }

    public CreatorStatusTargetResponse? NextStatus { get; init; }

    /// <summary>
    /// Which version of the ladder decided the status. Served so a client can tell a changed rule
    /// from a changed member.
    /// </summary>
    public required string StatusPolicyVersion { get; init; }

    public string? CountryCode { get; init; }

    public required IReadOnlyList<Guid> CommunityIds { get; init; }

    public required IReadOnlyList<string> Languages { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required IReadOnlyList<string> Expertise { get; init; }

    public required bool IsDiscoverable { get; init; }

    public required bool IsVerifiedCreator { get; init; }

    /// <summary>What the platform is restricting, in words a member may read. Never a risk signal.</summary>
    public string? StandingReason { get; init; }

    public required CreatorAudienceResponse Audience { get; init; }

    public required CreatorCompletenessResponse Completeness { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// CREATOR-GAP-001, -014. The public view.
/// <para>
/// <b>A strict subset, and the fields are absent rather than nulled.</b> §17 of the phase brief lists
/// what must not appear: contact details, verification evidence, moderation notes, risk information,
/// reward data, financial data, referral data and grant applications. None of them is on this type,
/// so a handler rendering it cannot leak one by forgetting.
/// </para>
/// </summary>
public sealed record PublicCreatorResponse
{
    public required Guid CreatorId { get; init; }

    /// <summary>
    /// The member behind the creator record. Public by design: it is the identifier every byline
    /// and follow button already carries, and the one <c>/creators/by-user/{userId}</c> answers to.
    /// </summary>
    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? Handle { get; init; }

    public string? AvatarUrl { get; init; }

    public string? Bio { get; init; }

    public required string Status { get; init; }

    public required bool IsVerifiedCreator { get; init; }

    public string? CountryCode { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }

    public required IReadOnlyList<string> Expertise { get; init; }

    public required IReadOnlyList<string> Languages { get; init; }

    public required long Followers { get; init; }

    public required long ContentPublished { get; init; }

    /// <summary>The public contribution figure the leaderboards rank on.</summary>
    public long? ReputationScore { get; init; }

    public required IReadOnlyList<CreatorBadgeResponse> Badges { get; init; }
}

/// <summary>CREATOR-GAP-014. A page of the public directory, with facets.</summary>
public sealed record CreatorDirectoryResponse
{
    public required IReadOnlyList<PublicCreatorResponse> Creators { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required CreatorFacetsResponse Facets { get; init; }
}

/// <summary>Facet counts, computed under the same visibility rule the search applies.</summary>
public sealed record CreatorFacetsResponse
{
    public required IReadOnlyList<FacetCountResponse> Countries { get; init; }

    public required IReadOnlyList<FacetCountResponse> Categories { get; init; }

    public required IReadOnlyList<FacetCountResponse> Expertise { get; init; }

    public required IReadOnlyList<FacetCountResponse> Languages { get; init; }

    public required int VerifiedCount { get; init; }

    public required int TotalCount { get; init; }
}

public sealed record FacetCountResponse(string Value, int Count);

// ── Verification ────────────────────────────────────────────────────────────────────────────────

/// <summary>A piece of supporting material, by reference. Carries nothing sensitive.</summary>
public sealed record CreatorEvidenceResponse
{
    public required Guid EvidenceId { get; init; }

    public required string Kind { get; init; }

    public required string Label { get; init; }

    public required string Status { get; init; }

    public string? RejectionReason { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}

/// <summary>CREATOR-GAP-002. The creator-programme application as the member sees it.</summary>
public sealed record CreatorVerificationResponse
{
    public Guid? ApplicationId { get; init; }

    public required string State { get; init; }

    /// <summary>Every requirement, with <c>isMet</c> decided against real state.</summary>
    public required IReadOnlyList<CreatorRequirementResponse> Requirements { get; init; }

    public required IReadOnlyList<CreatorEvidenceResponse> Evidence { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? ReviewStartedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? DecisionReason { get; init; }

    public IReadOnlyList<string>? InformationRequested { get; init; }

    public required bool CanResubmit { get; init; }

    public required bool CanAppeal { get; init; }

    public DateTimeOffset? RespondByAt { get; init; }
}

// ── Milestones ──────────────────────────────────────────────────────────────────────────────────

public sealed record CreatorRewardSummaryResponse
{
    public required string Kind { get; init; }

    public int? Points { get; init; }

    public string? BadgeKey { get; init; }

    public string? EntitlementKey { get; init; }

    public required string Label { get; init; }
}

/// <summary>CREATOR-GAP-003. One milestone and this creator's standing against it.</summary>
public sealed record CreatorMilestoneResponse
{
    public required Guid MilestoneId { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Icon { get; init; }

    public string? Category { get; init; }

    public required string State { get; init; }

    /// <summary>Weighted and capped by the server. Never <c>current / target</c> on a device.</summary>
    public required CreatorProgressResponse Progress { get; init; }

    public CreatorRewardSummaryResponse? Reward { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public DateTimeOffset? ClaimedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required int SortOrder { get; init; }
}

// ── Challenges ──────────────────────────────────────────────────────────────────────────────────

public sealed record CreatorChallengeTaskResponse
{
    public required Guid TaskId { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public required bool IsComplete { get; init; }

    public CreatorProgressResponse? Progress { get; init; }

    public string? ActionRoute { get; init; }

    public string? ActionLabel { get; init; }
}

/// <summary>CREATOR-GAP-004. A challenge, resolved against this creator's enrolment.</summary>
public sealed record CreatorChallengeResponse
{
    public required Guid ChallengeId { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Icon { get; init; }

    public string? Category { get; init; }

    /// <summary>The one state, resolved from the challenge's half and the member's.</summary>
    public required string State { get; init; }

    public required bool IsEligible { get; init; }

    public string? IneligibleReason { get; init; }

    /// <summary>The rules, as data. The client renders a list, never a switch statement.</summary>
    public required IReadOnlyList<CreatorChallengeTaskResponse> Tasks { get; init; }

    public required CreatorProgressResponse Progress { get; init; }

    public CreatorRewardSummaryResponse? Reward { get; init; }

    public required IReadOnlyList<string> Rules { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public int? ParticipantCount { get; init; }

    public required bool HasLeaderboard { get; init; }
}

// ── Leaderboards ────────────────────────────────────────────────────────────────────────────────

public sealed record CreatorLeaderboardEntryResponse
{
    public required int Rank { get; init; }

    public required Guid CreatorId { get; init; }

    public required string DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public required long Score { get; init; }

    public required bool IsVerifiedCreator { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// CREATOR-GAP-004, -012. A ranking.
/// <para>
/// <b><see cref="ScoreLabel"/> and <see cref="ScoreDefinition"/> are both served</b>, because the
/// register asks that "a rank is never a bare number" and a label alone does not achieve that.
/// </para>
/// <para>
/// <b><see cref="UpdatedAt"/> is when this ranking was computed, which is now.</b> The register
/// warns against "updated 3 hours ago" unless the server actually knows that; these boards are
/// computed on the request from the activity ledger, so the honest answer is the moment of the
/// request and <see cref="RefreshNote"/> says so.
/// </para>
/// </summary>
public sealed record CreatorLeaderboardResponse
{
    public required string Scope { get; init; }

    public required string Title { get; init; }

    public required string Period { get; init; }

    public required string PeriodLabel { get; init; }

    public required string ScoreLabel { get; init; }

    /// <summary>What the score counts, in the platform's own sentence.</summary>
    public required string ScoreDefinition { get; init; }

    public required IReadOnlyList<CreatorLeaderboardEntryResponse> Entries { get; init; }

    /// <summary>The caller's own row, even when it falls off the page. Null when they are unranked.</summary>
    public CreatorLeaderboardEntryResponse? ViewerEntry { get; init; }

    /// <summary>
    /// Whether the caller is excluded from public ranking by their own choice. Served so the app can
    /// explain an absent <see cref="ViewerEntry"/> instead of implying a zero score.
    /// </summary>
    public required bool ViewerIsOptedOut { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required string RefreshNote { get; init; }
}

// ── Grants ──────────────────────────────────────────────────────────────────────────────────────

public sealed record MoneyResponse(long MinorUnits, string Currency, int Exponent);

/// <summary>CREATOR-GAP-005. A grant programme.</summary>
public sealed record CommunityGrantResponse
{
    public required Guid GrantId { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    /// <summary>The programme's own state. <c>ClosingSoon</c> is decided here, never on a device.</summary>
    public required string State { get; init; }

    public required MoneyResponse FundingAmount { get; init; }

    public MoneyResponse? FundingMinimum { get; init; }

    public MoneyResponse? FundingMaximum { get; init; }

    public required IReadOnlyList<CreatorRequirementResponse> Requirements { get; init; }

    public required IReadOnlyList<string> Deliverables { get; init; }

    public required bool IsEligible { get; init; }

    public string? IneligibleReason { get; init; }

    public DateTimeOffset? OpensAt { get; init; }

    public DateTimeOffset? DeadlineAt { get; init; }

    public int? ApplicantCount { get; init; }

    public Guid? ApplicationId { get; init; }

    public string? ApplicationState { get; init; }

    public required IReadOnlyList<string> CountryCodes { get; init; }
}

public sealed record GrantFieldOptionResponse(string Value, string Label);

/// <summary>One question on a served grant form.</summary>
public sealed record GrantApplicationFieldResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? HelpText { get; init; }

    public required string Kind { get; init; }

    public required bool IsRequired { get; init; }

    public int? MaxLength { get; init; }

    public IReadOnlyList<GrantFieldOptionResponse>? Options { get; init; }
}

/// <summary>
/// CREATOR-GAP-005. The served application form.
/// <para>
/// The register calls this load-bearing: it is what makes adding a grant with eleven questions and a
/// dropdown a backend change rather than an app release.
/// </para>
/// </summary>
public sealed record GrantApplicationFormResponse
{
    public required Guid GrantId { get; init; }

    public required IReadOnlyList<GrantApplicationFieldResponse> Fields { get; init; }

    public required IReadOnlyList<GrantDeclarationResponse> Declarations { get; init; }

    public required bool AllowsAttachments { get; init; }

    public required bool AllowsAmountRequest { get; init; }
}

public sealed record GrantDeclarationResponse(string Key, string Text);

public sealed record GrantReportResponse
{
    public required Guid ReportId { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset DueAt { get; init; }

    public required string State { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }
}

/// <summary>CREATOR-GAP-005. One application, as the applicant sees it.</summary>
public sealed record GrantApplicationResponse
{
    public required Guid ApplicationId { get; init; }

    public required Guid GrantId { get; init; }

    public required string GrantTitle { get; init; }

    public required string State { get; init; }

    public required IReadOnlyDictionary<string, string> Answers { get; init; }

    public MoneyResponse? RequestedAmount { get; init; }

    public required IReadOnlyList<string> AttachmentIds { get; init; }

    public DateTimeOffset? SubmittedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionReason { get; init; }

    public IReadOnlyList<string>? InformationRequested { get; init; }

    public DateTimeOffset? RespondByAt { get; init; }

    public MoneyResponse? AwardedAmount { get; init; }

    /// <summary>Whether B15 has executed the funding. Null until it has — see the funding boundary.</summary>
    public string? FundingReference { get; init; }

    public required IReadOnlyList<GrantReportResponse> Reports { get; init; }

    public required bool CanWithdraw { get; init; }

    public required bool CanResubmit { get; init; }
}

// ── Academy ─────────────────────────────────────────────────────────────────────────────────────

public sealed record AcademySectionResponse
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public required IReadOnlyList<Guid> CourseIds { get; init; }

    public required int SortOrder { get; init; }
}

public sealed record CreatorCourseProgressResponse
{
    public required Guid CourseId { get; init; }

    public required string State { get; init; }

    public required int LessonsCompleted { get; init; }

    public required int LessonCount { get; init; }

    public required int Percentage { get; init; }

    public Guid? NextLessonId { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public Guid? CertificateId { get; init; }
}

public sealed record CreatorCourseResponse
{
    public required Guid CourseId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? SectionKey { get; init; }

    public required string Level { get; init; }

    public required int EstimatedMinutes { get; init; }

    public string? CoverImageUrl { get; init; }

    public string? Icon { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required int LessonCount { get; init; }

    public required bool AwardsCertificate { get; init; }

    public string? AwardsBadgeKey { get; init; }

    public required IReadOnlyList<string> PrerequisiteCourseSlugs { get; init; }

    public CreatorCourseProgressResponse? Progress { get; init; }

    public required bool IsSaved { get; init; }

    /// <summary>Bumped whenever the course or a lesson is rewritten. The versioning rule's handle.</summary>
    public required int ContentVersion { get; init; }

    public required int SortOrder { get; init; }
}

/// <summary>
/// One block of lesson content.
/// <para>
/// <b>A closed kind and bounded text. Never markup.</b> The register: "A content service that could
/// send markup could send a script."
/// </para>
/// </summary>
public sealed record LessonBlockResponse
{
    public required string Kind { get; init; }

    public string? Text { get; init; }

    public string? Title { get; init; }

    public string? Tone { get; init; }

    public bool? Ordered { get; init; }

    public string? Route { get; init; }

    public IReadOnlyList<string>? Items { get; init; }
}

/// <summary>One quiz option. <b>Carries no answer.</b></summary>
public sealed record LessonQuizOptionResponse(Guid OptionId, string Label);

public sealed record LessonQuizQuestionResponse
{
    public required Guid QuestionId { get; init; }

    public required string Prompt { get; init; }

    public required IReadOnlyList<LessonQuizOptionResponse> Options { get; init; }
}

/// <summary>
/// A quiz as the member sees it before answering.
/// <para>
/// <b>No <c>isCorrect</c> anywhere on this type or the ones it holds</b>, and no explanation either
/// — an explanation before answering is half the answer. Explanations arrive with the result.
/// </para>
/// </summary>
public sealed record LessonQuizResponse
{
    public required Guid QuizId { get; init; }

    public required int PassMark { get; init; }

    public required IReadOnlyList<LessonQuizQuestionResponse> Questions { get; init; }
}

public sealed record CreatorLessonResponse
{
    public required Guid LessonId { get; init; }

    public required Guid CourseId { get; init; }

    public required string Title { get; init; }

    public required string Kind { get; init; }

    public required int EstimatedMinutes { get; init; }

    public required IReadOnlyList<LessonBlockResponse> Blocks { get; init; }

    public LessonQuizResponse? Quiz { get; init; }

    public required bool IsComplete { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>The lesson's version now.</summary>
    public required int ContentVersion { get; init; }

    /// <summary>The version this member completed. Null when they have not. See the versioning rule.</summary>
    public int? CompletedAtVersion { get; init; }

    /// <summary>Whether the lesson has been rewritten since this member read it.</summary>
    public required bool HasBeenRevised { get; init; }

    public required int SortOrder { get; init; }
}

public sealed record QuizOutcomeResponse(Guid QuestionId, bool IsCorrect, string? Explanation);

/// <summary>What grading produced. The one place a correctness flag reaches a client.</summary>
public sealed record LessonQuizResultResponse
{
    public required bool Passed { get; init; }

    public required int CorrectCount { get; init; }

    public required int TotalCount { get; init; }

    public required IReadOnlyList<QuizOutcomeResponse> Outcomes { get; init; }

    public required bool CanRetry { get; init; }

    public DateTimeOffset? RetryAvailableAt { get; init; }

    /// <summary>The course progress after the attempt, so the client re-reads nothing.</summary>
    public CreatorCourseProgressResponse? Progress { get; init; }
}

public sealed record CreatorCertificateResponse
{
    public required Guid CertificateId { get; init; }

    public required Guid CourseId { get; init; }

    public required string CourseTitle { get; init; }

    public required string RecipientName { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public required string VerificationReference { get; init; }
}

// ── Referrals ───────────────────────────────────────────────────────────────────────────────────

/// <summary>CREATOR-GAP-009. The member's referral programme.</summary>
public sealed record CreatorReferralProgrammeResponse
{
    public required string Code { get; init; }

    /// <summary>Built by the server, so the domain is never guessed by a client.</summary>
    public required string ShareUrl { get; init; }

    public required string RewardDescription { get; init; }

    public required IReadOnlyList<string> Terms { get; init; }

    /// <summary>What makes a signup qualify, in the platform's own words.</summary>
    public required string QualificationDescription { get; init; }

    /// <summary>Which version of the rule the description above describes.</summary>
    public required string QualificationVersion { get; init; }

    public required int Invited { get; init; }

    public required int SignedUp { get; init; }

    public required int Qualified { get; init; }

    public required int PendingRewards { get; init; }

    public required int ConfirmedRewards { get; init; }

    public required long TotalPointsEarned { get; init; }

    public required bool IsEligible { get; init; }

    public string? IneligibleReason { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CreatorReferralResponse
{
    public required Guid ReferralId { get; init; }

    /// <summary>A display name the server chose to reveal. Never an email or a phone number.</summary>
    public required string InviteeLabel { get; init; }

    public required string State { get; init; }

    public string? StateReason { get; init; }

    public required DateTimeOffset InvitedAt { get; init; }

    public DateTimeOffset? SignedUpAt { get; init; }

    public DateTimeOffset? QualifiedAt { get; init; }

    public int? RewardPoints { get; init; }
}

public sealed record CreatorReferralPageResponse
{
    public required IReadOnlyList<CreatorReferralResponse> Referrals { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}

// ── Rewards ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>CREATOR-GAP-010. The sign-up bonus, as a state.</summary>
public sealed record SignupBonusResponse
{
    public required string State { get; init; }

    public required int Points { get; init; }

    /// <summary>Member-safe, always. Never names a risk check.</summary>
    public string? IneligibleReason { get; init; }

    public required IReadOnlyList<CreatorRequirementResponse> Requirements { get; init; }

    public DateTimeOffset? AwardedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>CREATOR-GAP-011. One rule saying what an action pays.</summary>
public sealed record CreatorPointRuleResponse
{
    public required string RuleKey { get; init; }

    public required string Label { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// The action's weight toward leaderboards, milestones and challenges. <b>Not reward points</b>:
    /// nothing pays it (RWD-02). What pays points is Trust's rules with <c>creditPoints</c>, and this
    /// programme's milestones, challenges and sign-up bonus.
    /// </summary>
    public required int Points { get; init; }

    public string? ActionRoute { get; init; }

    public string? LimitLabel { get; init; }

    public int? MaxOccurrences { get; init; }

    public int? CooldownHours { get; init; }

    public string? Category { get; init; }

    public required bool IsActive { get; init; }
}

/// <summary>
/// CREATOR-GAP-011. The rules catalogue, with its version and its expiry policy.
/// <para>
/// The register asks for a version "so a changed award value is visible rather than silently
/// different", and §14 of the phase brief asks for expiry to be stated rather than left silent.
/// </para>
/// </summary>
public sealed record CreatorPointRulesResponse
{
    public required IReadOnlyList<CreatorPointRuleResponse> Rules { get; init; }

    public required string CatalogueVersion { get; init; }

    public required string ExpiryPolicy { get; init; }

    public required string ExpiryDescription { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>CREATOR-GAP-011. Something points can be exchanged for.</summary>
public sealed record CreatorRewardResponse
{
    public required Guid RewardId { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>The authoritative price. A client never sends one.</summary>
    public required int PointsCost { get; init; }

    /// <summary>Available, Locked or Expired, decided against this caller.</summary>
    public required string State { get; init; }

    public string? Category { get; init; }

    public string? Icon { get; init; }

    /// <summary>Why it is locked, when it is. Never a risk signal.</summary>
    public string? LockedReason { get; init; }

    public string? GrantKind { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record CreatorRewardCatalogueResponse
{
    public required IReadOnlyList<CreatorRewardResponse> Rewards { get; init; }

    public required string CatalogueVersion { get; init; }

    /// <summary>The caller's spendable balance, read from B15. Never held by this module.</summary>
    public required long AvailablePoints { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record CreatorRewardRedemptionResponse
{
    public required Guid RedemptionId { get; init; }

    public required Guid RewardId { get; init; }

    /// <summary>The catalogue key, so the fulfilment queue knows what to issue.</summary>
    public string? RewardKey { get; init; }

    /// <summary>Who redeemed it. The caller's own id on their own list; the member on the staff queue.</summary>
    public Guid MemberUserId { get; init; }

    public required string RewardTitle { get; init; }

    public required int PointsSpent { get; init; }

    public required string State { get; init; }

    public string? GrantReference { get; init; }

    public string? RejectionReason { get; init; }

    public required string CatalogueVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
