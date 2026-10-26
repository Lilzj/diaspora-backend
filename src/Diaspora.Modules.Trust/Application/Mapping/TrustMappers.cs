using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;

namespace Diaspora.Modules.Trust.Application.Mapping;

/// <summary>
/// Turns aggregates into the DTOs the API returns.
/// <para>
/// Hand-written rather than Mapster-configured. Several of these mappings are not
/// property-to-property — badge labels are composed, flags are derived from country codes,
/// and the member-facing projections deliberately drop fields the staff ones keep. A mapping
/// library would hide exactly the decisions that most need to be visible in a module whose
/// job is deciding what people are allowed to see about each other.
/// </para>
/// </summary>
public static class TrustMappers
{
    // ── Verification ─────────────────────────────────────────────────────

    public static VerificationResponse ToResponse(this VerificationRequest request) =>
        new()
        {
            VerificationId = request.Id.Value,
            UserId = request.UserId.Value,
            Type = request.Type.ToString(),
            Status = request.Status.ToString(),
            SubmittedAt = request.SubmittedAt,
            ReviewedAt = request.ReviewedAt,
            ExpiresAt = request.ExpiresAt,
            RejectionReasonCode = request.RejectionReason?.ToString(),
            RejectionReasonDetails = request.RejectionDetails,
            Nationality = request.Nationality,
            ResidenceCountry = request.ResidenceCountry,
            TargetCountry = request.TargetCountry,
            ArrivalDate = request.ArrivalDate,
            ExpertiseAreas = request.ExpertiseAreas,
            DocumentCount = request.Documents.Count,
            CanAppeal = VerificationWorkflow.IsAppealable(request.Status)
        };

    /// <summary>
    /// The detail projection.
    /// </summary>
    /// <param name="forStaff">
    /// Staff see reviewer identities and internal notes. A member sees neither: knowing which
    /// named reviewer rejected them invites pressure on that person, and internal notes are
    /// written on the assumption they are internal.
    /// </param>
    public static VerificationDetailResponse ToDetailResponse(
        this VerificationRequest request,
        IReadOnlyDictionary<Guid, string>? documentUrls = null,
        bool forStaff = false) =>
        new()
        {
            Verification = request.ToResponse(),
            Documents = request.Documents
                .Select(document => document.ToResponse(
                    documentUrls is not null && documentUrls.TryGetValue(document.Id.Value, out var url)
                        ? url
                        : null))
                .ToList(),
            Reviews = request.Reviews
                .OrderBy(review => review.DecidedAt)
                .Select(review => review.ToResponse(forStaff))
                .ToList()
        };

    public static VerificationDocumentResponse ToResponse(
        this VerificationDocument document, string? presignedUrl = null) =>
        new()
        {
            DocumentId = document.Id.Value,
            DocumentType = document.DocumentType.ToString(),
            Status = document.Status.ToString(),
            DocumentUrl = presignedUrl,
            OriginalFileName = document.OriginalFileName,
            SizeInBytes = document.SizeInBytes,
            DocumentNumber = document.DocumentNumber,
            IssuingCountry = document.IssuingCountry,
            IssuedOn = document.IssuedOn,
            ExpiresOn = document.ExpiresOn,
            Version = document.Version,
            ReviewComment = document.ReviewComment,
            UploadedAt = document.UploadedAt
        };

    public static VerificationReviewResponse ToResponse(
        this VerificationReview review, bool forStaff) =>
        new()
        {
            ReviewId = review.Id.Value,
            DecidedBy = forStaff
                ? review.DecidedByName
                // A member is told whether a human or a rule decided, never who.
                : review.DecisionMode == VerificationDecisionMode.ManualReview
                    ? "Compliance team"
                    : review.DecidedByName,
            FromStatus = review.FromStatus.ToString(),
            ToStatus = review.ToStatus.ToString(),
            DecisionMode = review.DecisionMode.ToString(),
            RejectionReason = review.RejectionReason?.ToString(),
            Notes = forStaff ? review.Notes : null,
            DecidedAt = review.DecidedAt
        };

    public static VerificationQueueItemResponse ToQueueItem(
        this VerificationRequest request,
        DateTimeOffset asOf,
        string? submitterTier = null,
        int submitterViolations = 0) =>
        new()
        {
            VerificationId = request.Id.Value,
            UserId = request.UserId.Value,
            Type = request.Type.ToString(),
            Status = request.Status.ToString(),
            SubmittedAt = request.SubmittedAt,
            ReviewDueAt = request.ReviewDueAt,
            IsOverdue = request.IsOverdue(asOf),
            AssignedReviewerId = request.AssignedReviewerId?.Value,
            DocumentCount = request.Documents.Count,
            TargetCountry = request.TargetCountry,
            SubmitterTier = submitterTier,
            SubmitterViolationCount = submitterViolations
        };

    // ── Trust profile ────────────────────────────────────────────────────

    public static TrustProfileResponse ToResponse(
        this TrustProfile profile,
        IReadOnlyDictionary<string, string> verifications,
        DateTimeOffset asOf,
        bool includeRisk) =>
        new()
        {
            UserId = profile.UserId.Value,
            TrustLevel = profile.Tier.ToString(),
            HealthStatus = profile.ToHealthResponse(),
            RiskScore = includeRisk ? profile.Risk.Score : null,
            RiskLevel = includeRisk ? profile.Risk.Level.ToString() : null,
            Verifications = verifications,
            Badges = profile.Badges
                .Where(badge => badge.IsActive)
                .OrderBy(badge => badge.AwardedAt)
                .Select(badge => badge.ToResponse())
                .ToList(),
            CountryReputations = profile.Countries
                .OrderByDescending(country => country.Points)
                .Select(country => country.ToResponse())
                .ToList(),
            ExperienceTimeline = profile.Countries
                .Where(country => country.ArrivedOn is not null)
                .OrderBy(country => country.ArrivedOn)
                .Select(country => country.ToExperienceResponse(asOf))
                .ToList(),
            JoinedAt = profile.JoinedAt,
            TrustLevelChangedAt = profile.TierChangedAt
        };

    public static PublicTrustProfileResponse ToPublicResponse(
        this TrustProfile profile, ReputationLevel reputationLevel) =>
        new()
        {
            UserId = profile.UserId.Value,
            TrustLevel = profile.Tier.ToString(),
            ReputationLevel = reputationLevel.ToString(),
            Badges = profile.Badges
                .Where(badge => badge.IsActive)
                .OrderBy(badge => badge.AwardedAt)
                .Select(badge => badge.ToResponse())
                .ToList(),
            CountryReputations = profile.Countries
                .Where(country => country.IsVerified)
                .OrderByDescending(country => country.Points)
                .Select(country => country.ToResponse())
                .ToList(),
            IsVerifiedMember = profile.Tier >= TrustTier.Verified,
            MemberSince = profile.JoinedAt
        };

    public static AccountHealthStatusResponse ToHealthResponse(this TrustProfile profile) =>
        new()
        {
            Health = profile.Health.ToString(),
            ViolationsCount = profile.ViolationCount,
            WarningsCount = profile.WarningCount,
            ActiveAppealsCount = profile.ActiveAppealCount,
            SpamReportsCount = profile.SpamReportCount,
            Reasons = string.IsNullOrWhiteSpace(profile.HealthReasons)
                ? []
                : [profile.HealthReasons]
        };

    public static BadgeResponse ToResponse(this BadgeAward award) =>
        new()
        {
            Type = award.Badge.ToString(),
            Label = BadgeLabels.For(award.Badge, award.CountryCode),
            Since = award.AwardedAt.Year,
            CountryCode = award.CountryCode,
            AwardedAt = award.AwardedAt
        };

    public static CountryStandingResponse ToResponse(this CountryStanding standing) =>
        new()
        {
            CountryCode = standing.CountryCode,
            CountryName = CountryCatalog.NameFor(standing.CountryCode),
            Flag = CountryCatalog.FlagFor(standing.CountryCode),
            Points = standing.Points,
            Level = standing.Level.ToString(),
            Stars = standing.Stars,
            IsVerified = standing.IsVerified
        };

    public static CountryExperienceResponse ToExperienceResponse(
        this CountryStanding standing, DateTimeOffset asOf) =>
        new()
        {
            CountryCode = standing.CountryCode,
            CountryName = CountryCatalog.NameFor(standing.CountryCode),
            Flag = CountryCatalog.FlagFor(standing.CountryCode),
            StartYear = standing.ArrivedOn?.Year ?? asOf.Year,
            EndYear = standing.DepartedOn?.Year,
            IsVerified = standing.IsVerified,
            Role = standing.Role ?? "Diaspora Member",
            YearsOfResidence = standing.YearsOfResidence(DateOnly.FromDateTime(asOf.UtcDateTime))
        };

    public static TrustTimelineEventResponse ToResponse(this TrustEvent trustEvent) =>
        new()
        {
            EventId = trustEvent.Id.Value,
            Type = trustEvent.Type,
            Label = trustEvent.Label,
            Timestamp = trustEvent.OccurredAt,
            Icon = trustEvent.Icon ?? TrustEventTypes.IconFor(trustEvent.Type),
            Description = trustEvent.Description,
            CountryCode = trustEvent.CountryCode
        };

    // ── Reputation ───────────────────────────────────────────────────────

    public static ReputationProfileResponse ToResponse(
        this ReputationProfile reputation,
        IReadOnlyList<BadgeResponse> badges,
        IReadOnlyList<ReputationDimensionBreakdownResponse> breakdown,
        DateTimeOffset asOf)
    {
        var next = reputation.NextLevel;

        return new ReputationProfileResponse
        {
            UserId = reputation.UserId.Value,
            ContributionScore = reputation.ContributionScore,
            VerificationScore = reputation.VerificationScore,
            TrustScore = reputation.TrustScore,
            CombinedScore = reputation.CombinedScore,
            Level = reputation.Level.ToString(),
            NextLevel = next is null
                ? null
                : new NextLevelResponse
                {
                    Name = next.Value.Level.ToString(),
                    MinScore = next.Value.MinScore,
                    PointsNeeded = next.Value.PointsNeeded
                },
            GuidesPublished = reputation.GuidesPublished,
            GuidesApproved = reputation.GuidesApproved,
            UpdatesPublished = reputation.UpdatesPublished,
            UpdatesVerified = reputation.UpdatesVerified,
            CommentsWritten = reputation.CommentsWritten,
            GuidesUpdated = reputation.GuidesUpdated,
            PhoneVerified = reputation.PhoneVerified,
            EmailVerified = reputation.EmailVerified,
            GovernmentIdVerified = reputation.GovernmentIdVerified,
            ProfilePhotoAdded = reputation.ProfilePhotoAdded,
            ProfileCompleted = reputation.ProfileCompleted,
            ProviderVerified = reputation.ProviderVerified,
            CountryVerificationsCount = reputation.CountryVerificationsCount,
            HelpfulVotesReceived = reputation.HelpfulVotesReceived,
            CommunityConfirmationsReceived = reputation.CommunityConfirmationsReceived,
            ViolationCount = reputation.ViolationCount,
            ReportsResolved = reputation.ReportsResolved,
            NoViolationStreakDays = reputation.NoViolationStreakDays(asOf),
            Badges = badges,
            LevelBenefits = reputation.Benefits.ToResponse(),
            LastCalculatedAt = reputation.LastCalculatedAt,
            LevelChangedAt = reputation.LevelChangedAt,
            Breakdown = breakdown
        };
    }

    /// <summary>
    /// The three dimension cards' rows, from the totals the scoring history recorded.
    /// <para>
    /// Every dimension is present even with no history, so the screen draws three cards for a new
    /// member as it always has. The adjustment is whatever the rows do not account for; see
    /// <see cref="ReputationDimensionBreakdownResponse"/>.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReputationDimensionBreakdownResponse> ToBreakdown(
        this ReputationProfile reputation,
        IReadOnlyList<ReputationEventSummary> summaries,
        ReputationRuleCatalog rules)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        ArgumentNullException.ThrowIfNull(rules);

        return
        [
            Dimension(ReputationDimension.contribution, reputation.ContributionScore),
            Dimension(ReputationDimension.verification, reputation.VerificationScore),
            Dimension(ReputationDimension.trust, reputation.TrustScore)
        ];

        ReputationDimensionBreakdownResponse Dimension(ReputationDimension dimension, int score)
        {
            // An action that was done and then reversed nets to nothing (D-9), and says nothing.
            var lines = summaries
                .Where(summary => summary.Dimension == dimension
                    && (summary.Count != 0 || summary.Points != 0))
                .Select(summary => new ReputationBreakdownLineResponse
                {
                    ActionType = summary.ActionType,
                    Label = rules.LabelFor(summary.ActionType),
                    Count = summary.Count,
                    Points = summary.Points
                })
                .OrderByDescending(line => line.Points)
                .ThenBy(line => line.Label, StringComparer.Ordinal)
                .ToList();

            return new ReputationDimensionBreakdownResponse
            {
                Dimension = dimension.ToString(),
                Score = score,
                Lines = lines,
                Adjustment = score - lines.Sum(line => line.Points)
            };
        }
    }

    public static ReputationRuleResponse ToResponse(this ReputationRule rule, string label) =>
        new()
        {
            ActionType = rule.ActionType,
            Label = label,
            Dimension = rule.Dimension.ToString(),
            Points = rule.Points,
            MaxOccurrences = rule.MaxOccurrences,
            CooldownHours = rule.CooldownHours,
            IsCountryScoped = rule.IsCountryScoped,
            CreditPoints = rule.CreditPoints
        };

    public static LevelBenefitsResponse ToResponse(this LevelBenefits benefits) =>
        new()
        {
            CanPublishImmediately = benefits.CanPublishImmediately,
            CanModerate = benefits.CanModerate,
            IsFeaturedEligible = benefits.IsFeaturedEligible,
            IsRewardsEligible = benefits.IsRewardsEligible,
            HasBetaAccess = benefits.HasBetaAccess,
            HasExclusiveBadge = benefits.HasExclusiveBadge,
            SearchBoostFactor = benefits.SearchBoostFactor
        };

    public static ReputationEventResponse ToResponse(
        this ReputationEvent reputationEvent, Guid userId) =>
        new()
        {
            EventId = reputationEvent.Id.Value,
            UserId = userId,
            EventType = reputationEvent.ActionType,
            Dimension = reputationEvent.Dimension.ToString(),
            Points = reputationEvent.Points,
            SourceType = reputationEvent.SourceType,
            SourceId = reputationEvent.SourceId,
            CountryCode = reputationEvent.CountryCode,
            Description = reputationEvent.Description,
            RuleSetVersion = reputationEvent.RuleSetVersion,
            CreatedAt = reputationEvent.CreatedAt
        };

    // ── Appeals ──────────────────────────────────────────────────────────

    public static AppealResponse ToResponse(this Appeal appeal) =>
        new()
        {
            AppealId = appeal.Id.Value,
            Type = appeal.Type.ToString(),
            TargetId = appeal.TargetId,
            Reason = appeal.Reason,
            Status = appeal.Status.ToString(),
            Explanation = appeal.Explanation,
            IsCorrectionRequest = appeal.IsCorrectionRequest,
            EvidenceCount = appeal.EvidenceKeys.Count,
            CreatedAt = appeal.SubmittedAt,
            ResolvedAt = appeal.ResolvedAt
        };

    public static AppealDetailResponse ToDetailResponse(this Appeal appeal, bool forStaff) =>
        new()
        {
            Appeal = appeal.ToResponse(),
            Reviews = appeal.Reviews
                .OrderBy(review => review.ReviewedAt)
                .Select(review => new AppealReviewResponse
                {
                    ReviewId = review.Id.Value,
                    ReviewerId = forStaff ? review.ReviewerId.Value : null,
                    FromStatus = review.FromStatus.ToString(),
                    ToStatus = review.ToStatus.ToString(),
                    Notes = review.Notes,
                    ReviewedAt = review.ReviewedAt
                })
                .ToList()
        };

    // ── Moderation ───────────────────────────────────────────────────────

    public static ModerationQueueItemResponse ToResponse(
        this ModerationItem item,
        DateTimeOffset asOf,
        string? authorLevel = null,
        int authorViolations = 0) =>
        new()
        {
            ItemId = item.Id.Value,
            ContentType = item.ContentType.ToString(),
            ContentId = item.ContentId,
            Title = item.Title,
            AuthorId = item.AuthorId.Value,
            AuthorLevel = authorLevel,
            AuthorViolationCount = authorViolations,
            AiReview = item.AiReviewClean is null
                ? null
                : new AiReviewResponse
                {
                    Status = item.AiReviewClean.Value ? "Clean" : "Flagged",
                    Flags = item.AiFlags,
                    Confidence = item.AiConfidence ?? 0m,
                    ProcessedAt = item.AiReviewedAt ?? asOf
                },
            ReportCount = item.ReportCount,
            Status = item.Status.ToString(),
            Priority = item.Priority.ToString(),
            AssignedModeratorId = item.AssignedModeratorId?.Value,
            DueAt = item.DueAt,
            IsOverdue = item.IsOverdue(asOf),
            CreatedAt = item.CreatedAt
        };

    public static ModerationDecisionResponse ToResponse(
        this ModerationDecision decision,
        ModerationContentType contentType,
        string contentId) =>
        new()
        {
            ActionId = decision.Id.Value,
            ModeratorId = decision.ModeratorId.Value,
            ContentType = contentType.ToString(),
            ContentId = contentId,
            ActionType = decision.Action.ToString(),
            Reason = decision.Reason,
            PreviousStatus = decision.PreviousStatus.ToString(),
            NewStatus = decision.NewStatus.ToString(),
            CreatedAt = decision.DecidedAt
        };

    // ── Eligibility ──────────────────────────────────────────────────────

    public static EligibilityDecisionResponse ToResponse(this EligibilityDecision decision) =>
        new()
        {
            Allowed = decision.Allowed,
            Code = decision.Code.ToString(),
            Reason = decision.Reason,
            WaitTimeSec = decision.WaitTimeSeconds,
            UnmetRequirements = decision.UnmetRequirements
        };

    public static FeatureRequirementResponse ToResponse(this CapabilityProgress progress) =>
        new()
        {
            FeatureId = progress.CapabilityKey,
            Name = progress.Name,
            IsUnlocked = progress.IsUnlocked,
            Requirements = progress.Requirements
                .Select(item => new RequirementItemResponse
                {
                    Description = item.Description,
                    IsMet = item.IsMet,
                    CurrentProgress = item.CurrentProgress
                })
                .ToList()
        };
}

/// <summary>
/// Display text for badges.
/// <para>
/// Composed server-side because the frontend's <c>ReputationBadge.label</c> already arrives
/// pre-rendered with an emoji and, for country badges, a flag and country name — see the
/// seeded <c>'🇬🇧 UK Verified'</c>. Building the string here keeps a new badge from rendering
/// blank until the app is re-released.
/// </para>
/// </summary>
public static class BadgeLabels
{
    public static string For(BadgeType badge, string? countryCode)
    {
        if (badge == BadgeType.CountryVerified && !string.IsNullOrWhiteSpace(countryCode))
        {
            return $"{CountryCatalog.FlagFor(countryCode)} {CountryCatalog.NameFor(countryCode)} Verified";
        }

        var label = badge switch
        {
            BadgeType.PhoneVerified => "📱 Phone Verified",
            BadgeType.EmailVerified => "📧 Email Verified",
            BadgeType.GovernmentIdVerified => "🆔 Government ID Verified",
            BadgeType.ProfilePhoto => "📷 Profile Photo",
            BadgeType.ProviderVerified => "💼 Provider Verified",
            BadgeType.TopContributor => "🏆 Top Contributor",
            BadgeType.VerifiedMember => "🛡️ Verified Member",
            BadgeType.TrustedContributor => "🏅 Trusted Contributor",
            BadgeType.CommunityGuide => "🧭 Community Guide",
            BadgeType.VerifiedBusiness => "🏢 Verified Business",
            BadgeType.VerifiedOrganization => "🏛️ Verified Organization",
            BadgeType.EarlyMember => "🌱 Early Member",
            BadgeType.Moderator => "⚖️ Moderator",
            BadgeType.CountryVerified => "🌍 Country Verified",
            _ => badge.ToString()
        };

        return string.IsNullOrWhiteSpace(countryCode)
            ? label
            : $"{CountryCatalog.FlagFor(countryCode)} {label}";
    }
}
