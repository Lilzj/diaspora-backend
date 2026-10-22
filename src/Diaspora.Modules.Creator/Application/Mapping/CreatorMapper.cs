using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Academy;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Referrals;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Modules.Creator.Domain.ValueObjects;
using Diaspora.Modules.Creator.Domain.Verification;

namespace Diaspora.Modules.Creator.Application.Mapping;

/// <summary>
/// Turns domain state into the wire contract.
/// <para>
/// <b>Static, and the class is what keeps two secrets.</b> There is no overload here that projects
/// an <c>AcademyQuizOption</c>'s <c>IsCorrect</c>, and no overload that projects a
/// <c>GrantDecisionRecord</c> or a <c>VerificationDecisionRecord</c>. Both omissions are the
/// enforcement: a handler cannot leak an answer key or an audit trail by forgetting to exclude it,
/// because there is nothing here that would include it.
/// </para>
/// <para>
/// <b>Everything that could be computed on a device is computed here instead.</b> A progress
/// percentage, a requirement's <c>isMet</c>, a challenge's resolved state, a grant's
/// <c>ClosingSoon</c> — all served, because the register's whole objection to the alternative is
/// that a client which computed them would disagree with the platform the day a rule changed.
/// </para>
/// </summary>
public static class CreatorMapper
{
    // ── Shared shapes ───────────────────────────────────────────────────────────────────────

    public static CreatorProgressResponse ToResponse(this CreatorProgress progress) =>
        new()
        {
            Current = progress.Current,
            Target = progress.Target,
            Percentage = progress.Percentage,
            Unit = progress.Unit
        };

    public static CreatorRequirementResponse ToResponse(this CreatorRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return new CreatorRequirementResponse
        {
            Key = requirement.Key,
            Label = requirement.Label,
            Description = requirement.Description,
            IsMet = requirement.IsMet,
            ActionRoute = requirement.ActionRoute,
            ActionLabel = requirement.ActionLabel,
            Current = requirement.Current,
            Target = requirement.Target
        };
    }

    public static IReadOnlyList<CreatorRequirementResponse> ToResponses(
        this IEnumerable<CreatorRequirement> requirements) =>
        requirements.Select(ToResponse).ToList();

    // ── CREATOR-GAP-001 ─────────────────────────────────────────────────────────────────────

    public static CreatorProfileResponse ToResponse(
        this CreatorProfile creator, IReadOnlyList<Guid> communityIds)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(communityIds);

        var next = CreatorStatusPolicy.NextTarget(creator);
        var (percentage, missing) = CreatorStatusPolicy.Completeness(creator);

        return new CreatorProfileResponse
        {
            CreatorId = creator.Id.Value,
            UserId = creator.UserId,
            DisplayName = creator.DisplayName,
            Handle = creator.Handle,
            AvatarUrl = creator.AvatarUrl,
            CoverImageUrl = creator.CoverImageUrl,
            Bio = creator.Bio,
            Status = creator.Status.ToString(),
            StatusChangedAt = creator.StatusChangedAt,
            NextStatus = next is null
                ? null
                : new CreatorStatusTargetResponse
                {
                    Status = next.Status.ToString(),
                    Label = next.Label,
                    Requirements = next.Requirements.ToResponses()
                },
            StatusPolicyVersion = CreatorStatusPolicy.Version,
            CountryCode = creator.CountryCode,
            CommunityIds = communityIds,
            Languages = creator.TagValues(CreatorTagKind.Language),
            Categories = creator.TagValues(CreatorTagKind.Category),
            Expertise = creator.TagValues(CreatorTagKind.Expertise),
            IsDiscoverable = creator.IsDiscoverable,
            IsVerifiedCreator = creator.IsVerifiedCreator,
            StandingReason = creator.StandingReason,
            Audience = new CreatorAudienceResponse
            {
                Followers = creator.Followers,
                Following = creator.Following,

                // Reach is B19's. Zero with the flag beside it says "not measured" rather than
                // "nobody" — see CreatorAudienceResponse.
                Reach = 0,
                ReachAvailable = false,
                TotalContentPublished = creator.TotalContentPublished,
                TotalHelpfulVotes = creator.TotalHelpfulVotes
            },
            Completeness = new CreatorCompletenessResponse
            {
                Percentage = percentage,
                Missing = missing.ToResponses()
            },
            JoinedAt = creator.JoinedAt,
            UpdatedAt = creator.UpdatedAt
        };
    }

    public static PublicCreatorResponse ToPublicResponse(
        this CreatorProfile creator, IReadOnlyList<CreatorBadgeResponse> badges)
    {
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentNullException.ThrowIfNull(badges);

        return new PublicCreatorResponse
        {
            CreatorId = creator.Id.Value,
            UserId = creator.UserId,
            DisplayName = creator.DisplayName,
            Handle = creator.Handle,
            AvatarUrl = creator.AvatarUrl,
            Bio = creator.Bio,
            Status = creator.Status.ToString(),
            IsVerifiedCreator = creator.IsVerifiedCreator,
            CountryCode = creator.CountryCode,
            Categories = creator.TagValues(CreatorTagKind.Category),
            Expertise = creator.TagValues(CreatorTagKind.Expertise),
            Languages = creator.TagValues(CreatorTagKind.Language),
            Followers = creator.Followers,
            ContentPublished = creator.TotalContentPublished,
            ReputationScore = creator.WeightedContributionScore,
            Badges = badges
        };
    }

    // ── CREATOR-GAP-002 ─────────────────────────────────────────────────────────────────────

    public static CreatorEvidenceResponse ToResponse(this CreatorVerificationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new CreatorEvidenceResponse
        {
            EvidenceId = evidence.EvidenceId,
            Kind = evidence.Kind,
            Label = evidence.Label,
            Status = evidence.Status.ToString(),
            RejectionReason = evidence.RejectionReason,
            UploadedAt = evidence.UploadedAt
        };
    }

    /// <summary>
    /// The application as the member sees it.
    /// <para>
    /// <b>The audit trail is absent by construction.</b> An applicant reading which reviewer refused
    /// them is a safety problem rather than a transparency win, and the way to guarantee it never
    /// happens is for this method not to know how to render one.
    /// </para>
    /// </summary>
    public static CreatorVerificationResponse ToResponse(
        this CreatorVerificationApplication? application,
        CreatorVerificationState state,
        IReadOnlyList<CreatorRequirement> requirements) =>
        new()
        {
            ApplicationId = application?.Id.Value,
            State = state.ToString(),
            Requirements = requirements.ToResponses(),
            Evidence = application?.Evidence.Select(ToResponse).ToList() ?? [],
            SubmittedAt = application?.SubmittedAt,
            ReviewStartedAt = application?.ReviewStartedAt,
            DecidedAt = application?.DecidedAt,
            ExpiresAt = application?.ExpiresAt,
            DecisionReason = application?.DecisionReason,
            InformationRequested = application?.InformationRequests.Count > 0
                ? application.InformationRequests.Select(request => request.Text).ToList()
                : null,
            CanResubmit = application?.CanResubmit ?? false,
            CanAppeal = application?.CanAppeal ?? false,
            RespondByAt = application?.RespondByAt
        };

    // ── CREATOR-GAP-003 ─────────────────────────────────────────────────────────────────────

    public static CreatorMilestoneResponse ToResponse(
        this CreatorMilestoneDefinition definition, CreatorMilestoneProgress? progress)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var figure = progress?.Progress(definition.Unit)
            ?? CreatorProgress.Of(0, definition.Target, definition.Unit);

        return new CreatorMilestoneResponse
        {
            MilestoneId = definition.Id.Value,
            Key = definition.Key,
            Title = definition.Title,
            Description = definition.Description,
            Icon = definition.Icon,
            Category = definition.Category,
            State = (progress?.State ?? CreatorMilestoneState.Locked).ToString(),
            Progress = figure.ToResponse(),
            Reward = new CreatorRewardSummaryResponse
            {
                Kind = definition.RewardKind,
                Points = definition.RewardPoints > 0 ? definition.RewardPoints : null,
                BadgeKey = definition.RewardBadgeKey,
                EntitlementKey = definition.RewardEntitlementKey,
                Label = definition.RewardLabel
            },
            CompletedAt = progress?.CompletedAt,
            ClaimedAt = progress?.ClaimedAt,
            ExpiresAt = definition.ExpiresAt,
            SortOrder = definition.SortOrder
        };
    }

    // ── CREATOR-GAP-004 ─────────────────────────────────────────────────────────────────────

    public static CreatorChallengeResponse ToResponse(
        this CreatorChallenge challenge,
        CreatorChallengeEnrollment? enrollment,
        bool isEligible,
        string? ineligibleReason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var tasks = challenge.Tasks
            .OrderBy(task => task.SortOrder)
            .Select(task =>
            {
                var row = enrollment?.FindTask(task.TaskId);
                var figure = CreatorProgress.Of(row?.RawCount ?? 0, task.Target);

                return new CreatorChallengeTaskResponse
                {
                    TaskId = task.TaskId.Value,
                    Label = task.Label,
                    Description = task.Description,
                    IsComplete = figure.IsComplete,
                    Progress = figure.ToResponse(),
                    ActionRoute = task.ActionRoute,
                    ActionLabel = task.ActionLabel
                };
            })
            .ToList();

        var overall = enrollment?.OverallProgress(challenge)
            ?? CreatorProgress.Of(0, challenge.Tasks.Sum(task => task.Target));

        return new CreatorChallengeResponse
        {
            ChallengeId = challenge.Id.Value,
            Key = challenge.Key,
            Title = challenge.Title,
            Description = challenge.Description,
            Icon = challenge.Icon,
            Category = challenge.Category,
            State = ChallengeStateResolver.Resolve(challenge, enrollment, now).ToString(),
            IsEligible = isEligible,
            IneligibleReason = ineligibleReason,
            Tasks = tasks,
            Progress = overall.ToResponse(),
            Reward = new CreatorRewardSummaryResponse
            {
                Kind = challenge.RewardKind,
                Points = challenge.RewardPoints > 0 ? challenge.RewardPoints : null,
                BadgeKey = challenge.RewardBadgeKey,
                Label = challenge.RewardLabel
            },
            Rules = challenge.Rules.OrderBy(rule => rule.SortOrder)
                .Select(rule => rule.Text)
                .ToList(),
            StartsAt = challenge.StartsAt,
            EndsAt = challenge.EndsAt,
            JoinedAt = enrollment?.JoinedAt,
            CompletedAt = enrollment?.CompletedAt,
            ParticipantCount = challenge.ParticipantCount,
            HasLeaderboard = challenge.HasLeaderboard
        };
    }

    // ── CREATOR-GAP-005 ─────────────────────────────────────────────────────────────────────

    public static MoneyResponse ToMoney(long minorUnits, string currency, int exponent) =>
        new(minorUnits, currency, exponent);

    public static CommunityGrantResponse ToResponse(
        this CommunityGrant grant,
        GrantApplication? application,
        IReadOnlyList<CreatorRequirement> requirements,
        bool isEligible,
        string? ineligibleReason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new CommunityGrantResponse
        {
            GrantId = grant.Id.Value,
            Key = grant.Key,
            Title = grant.Title,
            Summary = grant.Summary,
            Description = grant.Description,
            Category = grant.Category,
            State = grant.StateAt(now).ToString(),
            FundingAmount = ToMoney(grant.FundingMinorUnits, grant.Currency, grant.Exponent),
            FundingMinimum = grant.FundingMinimumMinorUnits is { } minimum
                ? ToMoney(minimum, grant.Currency, grant.Exponent)
                : null,
            FundingMaximum = grant.FundingMaximumMinorUnits is { } maximum
                ? ToMoney(maximum, grant.Currency, grant.Exponent)
                : null,
            Requirements = requirements.ToResponses(),
            Deliverables = grant.Deliverables.OrderBy(item => item.SortOrder)
                .Select(item => item.Text)
                .ToList(),
            IsEligible = isEligible,
            IneligibleReason = ineligibleReason,
            OpensAt = grant.OpensAt,
            DeadlineAt = grant.DeadlineAt,
            ApplicantCount = grant.ApplicantCount,
            ApplicationId = application?.Id.Value,
            ApplicationState = application?.State.ToString(),
            CountryCodes = grant.Countries.Select(country => country.CountryCode).ToList()
        };
    }

    public static GrantApplicationFormResponse ToFormResponse(this CommunityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new GrantApplicationFormResponse
        {
            GrantId = grant.Id.Value,
            Fields = grant.Fields
                .OrderBy(field => field.SortOrder)
                .Select(field => new GrantApplicationFieldResponse
                {
                    Key = field.Key,
                    Label = field.Label,
                    HelpText = field.HelpText,
                    Kind = field.Kind.ToString(),
                    IsRequired = field.IsRequired,
                    MaxLength = field.MaxLength,
                    Options = field.Kind == GrantFieldKind.Select
                        ? grant.OptionsFor(field.Key)
                            .Select(option => new GrantFieldOptionResponse(option.Value, option.Label))
                            .ToList()
                        : null
                })
                .ToList(),
            Declarations = grant.Declarations
                .OrderBy(declaration => declaration.SortOrder)
                .Select(declaration =>
                    new GrantDeclarationResponse(declaration.Key, declaration.Text))
                .ToList(),
            AllowsAttachments = grant.AllowsAttachments,
            AllowsAmountRequest = grant.AllowsAmountRequest
        };
    }

    public static GrantApplicationResponse ToResponse(
        this GrantApplication application, CommunityGrant grant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(grant);

        return new GrantApplicationResponse
        {
            ApplicationId = application.Id.Value,
            GrantId = application.GrantId.Value,
            GrantTitle = grant.Title,
            State = application.State.ToString(),
            Answers = application.Answers.ToDictionary(
                answer => answer.FieldKey, answer => answer.Value, StringComparer.Ordinal),
            RequestedAmount = application.RequestedMinorUnits is { } requested
                ? ToMoney(requested, application.Currency ?? grant.Currency, grant.Exponent)
                : null,
            AttachmentIds = application.Attachments
                .Select(attachment => attachment.Reference)
                .ToList(),
            SubmittedAt = application.SubmittedAt,
            DecidedAt = application.DecidedAt,
            DecisionReason = application.DecisionReason,
            InformationRequested = application.InformationRequests.Count > 0
                ? application.InformationRequests.Select(request => request.Text).ToList()
                : null,
            RespondByAt = application.RespondByAt,
            AwardedAmount = application.AwardedMinorUnits is { } awarded
                ? ToMoney(awarded, application.Currency ?? grant.Currency, grant.Exponent)
                : null,
            FundingReference = application.FundingReference,
            Reports = application.Reports
                .OrderBy(report => report.DueAt)
                .Select(report => new GrantReportResponse
                {
                    ReportId = report.ReportId,
                    Title = report.Title,
                    DueAt = report.DueAt,

                    // Computed against now, so an unswept row still reads as overdue.
                    State = report.StateAt(now).ToString(),
                    SubmittedAt = report.SubmittedAt
                })
                .ToList(),
            CanWithdraw = application.CanWithdraw,
            CanResubmit = application.CanResubmit
        };
    }

    // ── CREATOR-GAP-006 ─────────────────────────────────────────────────────────────────────

    public static CreatorCourseResponse ToResponse(
        this AcademyCourse course,
        CreatorLearningProgress? progress,
        IReadOnlyList<AcademyLessonId> lessons)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(lessons);

        return new CreatorCourseResponse
        {
            CourseId = course.Id.Value,
            Slug = course.Slug,
            Title = course.Title,
            Summary = course.Summary,
            SectionKey = course.SectionKey,
            Level = course.Level.ToString(),
            EstimatedMinutes = course.EstimatedMinutes,
            CoverImageUrl = course.CoverImageUrl,
            Icon = course.Icon,
            Tags = course.Tags.OrderBy(tag => tag.SortOrder).Select(tag => tag.Value).ToList(),
            LessonCount = course.LessonCount,
            AwardsCertificate = course.AwardsCertificate,
            AwardsBadgeKey = course.AwardsBadgeKey,
            PrerequisiteCourseSlugs = course.Prerequisites
                .Select(prerequisite => prerequisite.CourseSlug)
                .ToList(),
            Progress = progress?.ToResponse(course, lessons),
            IsSaved = progress?.IsSaved ?? false,
            ContentVersion = course.ContentVersion,
            SortOrder = course.SortOrder
        };
    }

    public static CreatorCourseProgressResponse ToResponse(
        this CreatorLearningProgress progress,
        AcademyCourse course,
        IReadOnlyList<AcademyLessonId> lessons)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(lessons);

        var completed = lessons.Count(lessonId => progress.HasCompleted(lessonId));
        var figure = CreatorProgress.Of(completed, lessons.Count);

        return new CreatorCourseProgressResponse
        {
            CourseId = course.Id.Value,
            State = progress.State.ToString(),
            LessonsCompleted = completed,
            LessonCount = lessons.Count,
            Percentage = figure.Percentage,
            NextLessonId = progress.NextLesson(lessons)?.Value,
            StartedAt = progress.StartedAt,
            CompletedAt = progress.CompletedAt,
            CertificateId = progress.CertificateId?.Value
        };
    }

    /// <summary>
    /// A lesson, with the quiz stripped of its answers.
    /// <para>
    /// <paramref name="quiz"/> is passed already loaded because the grader needs the same aggregate;
    /// this method reads <c>Label</c> and <c>Prompt</c> and nothing else off it.
    /// </para>
    /// </summary>
    public static CreatorLessonResponse ToResponse(
        this AcademyLesson lesson, AcademyQuiz? quiz, CreatorLearningProgress? progress)
    {
        ArgumentNullException.ThrowIfNull(lesson);

        var completedVersion = progress?.CompletedAtVersion(lesson.Id);

        return new CreatorLessonResponse
        {
            LessonId = lesson.Id.Value,
            CourseId = lesson.CourseId.Value,
            Title = lesson.Title,
            Kind = lesson.Kind.ToString(),
            EstimatedMinutes = lesson.EstimatedMinutes,
            Blocks = lesson.Blocks
                .OrderBy(block => block.SortOrder)
                .Select(block => block.ToResponse(lesson))
                .ToList(),
            Quiz = quiz?.ToResponse(),
            IsComplete = completedVersion is not null,
            CompletedAt = progress?.CompletedLessons
                .FirstOrDefault(done => done.LessonId == lesson.Id)?.CompletedAt,
            ContentVersion = lesson.ContentVersion,
            CompletedAtVersion = completedVersion,
            HasBeenRevised = completedVersion is { } version && version < lesson.ContentVersion,
            SortOrder = lesson.SortOrder
        };
    }

    private static LessonBlockResponse ToResponse(this LessonBlock block, AcademyLesson lesson) =>
        new()
        {
            Kind = block.Kind switch
            {
                LessonBlockKind.Heading => "heading",
                LessonBlockKind.Paragraph => "paragraph",
                LessonBlockKind.List => "list",
                LessonBlockKind.Callout => "callout",
                LessonBlockKind.Checklist => "checklist",
                LessonBlockKind.Quote => "quote",
                LessonBlockKind.Action => "action",
                _ => "paragraph"
            },
            Text = block.Text,
            Title = block.Title,
            Tone = block.Tone?.ToString().ToLowerInvariant(),
            Ordered = block.Kind == LessonBlockKind.List ? block.IsOrdered : null,
            Route = block.Route,
            Items = block.Kind is LessonBlockKind.List or LessonBlockKind.Checklist
                ? lesson.ItemsFor(block.SortOrder)
                : null
        };

    /// <summary>
    /// The quiz without its answer key.
    /// <para>
    /// <b>There is deliberately no overload that includes <c>IsCorrect</c>.</b> The explanation is
    /// left out too — an explanation shown before answering is half the answer — and both arrive
    /// with the graded result instead.
    /// </para>
    /// </summary>
    public static LessonQuizResponse ToResponse(this AcademyQuiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        return new LessonQuizResponse
        {
            QuizId = quiz.Id.Value,
            PassMark = quiz.PassMark,
            Questions = quiz.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new LessonQuizQuestionResponse
                {
                    QuestionId = question.QuestionId.Value,
                    Prompt = question.Prompt,
                    Options = quiz.OptionsFor(question.QuestionId)
                        .Select(option => new LessonQuizOptionResponse(option.OptionId, option.Label))
                        .ToList()
                })
                .ToList()
        };
    }

    public static CreatorCertificateResponse ToResponse(this CreatorCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new CreatorCertificateResponse
        {
            CertificateId = certificate.Id.Value,
            CourseId = certificate.CourseId.Value,
            CourseTitle = certificate.CourseTitle,
            RecipientName = certificate.RecipientName,
            IssuedAt = certificate.IssuedAt,
            VerificationReference = certificate.VerificationReference
        };
    }

    // ── CREATOR-GAP-009 ─────────────────────────────────────────────────────────────────────

    public static CreatorReferralResponse ToResponse(this CreatorReferral referral)
    {
        ArgumentNullException.ThrowIfNull(referral);

        return new CreatorReferralResponse
        {
            ReferralId = referral.Id.Value,
            InviteeLabel = referral.InviteeLabel,
            State = referral.State.ToString(),
            StateReason = referral.StateReason,
            InvitedAt = referral.InvitedAt,
            SignedUpAt = referral.SignedUpAt,
            QualifiedAt = referral.QualifiedAt,
            RewardPoints = referral.RewardPoints > 0 ? referral.RewardPoints : null
        };
    }

    // ── CREATOR-GAP-010, -011 ───────────────────────────────────────────────────────────────

    public static SignupBonusResponse ToResponse(
        this SignupBonus bonus, IReadOnlyList<CreatorRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(bonus);

        return new SignupBonusResponse
        {
            State = bonus.State.ToString(),
            Points = bonus.Points,
            IneligibleReason = bonus.MemberSafeReason,
            Requirements = requirements.ToResponses(),
            AwardedAt = bonus.AwardedAt,
            ExpiresAt = bonus.ExpiresAt
        };
    }

    public static CreatorPointRuleResponse ToResponse(this CreatorPointRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new CreatorPointRuleResponse
        {
            RuleKey = rule.Key,
            Label = rule.Label,
            Description = rule.Description,
            Points = rule.Points,
            ActionRoute = rule.ActionRoute,
            LimitLabel = rule.LimitLabel,
            MaxOccurrences = rule.MaxOccurrences,
            CooldownHours = rule.CooldownHours,
            Category = rule.Category,
            IsActive = rule.IsActive
        };
    }

    public static CreatorRewardResponse ToResponse(
        this CreatorRewardDefinition reward, CreatorRewardState state, string? lockedReason)
    {
        ArgumentNullException.ThrowIfNull(reward);

        return new CreatorRewardResponse
        {
            RewardId = reward.Id.Value,
            Key = reward.Key,
            Title = reward.Title,
            Description = reward.Description,
            PointsCost = reward.PointsCost,
            State = state.ToString(),
            Category = reward.Category,
            Icon = reward.Icon,
            LockedReason = lockedReason,
            GrantKind = reward.GrantKind,
            ExpiresAt = reward.AvailableUntil
        };
    }

    public static CreatorRewardRedemptionResponse ToResponse(this CreatorRewardRedemption redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        return new CreatorRewardRedemptionResponse
        {
            RedemptionId = redemption.Id.Value,
            RewardId = redemption.RewardId.Value,
            RewardKey = redemption.RewardKey,
            MemberUserId = redemption.UserId,
            RewardTitle = redemption.RewardTitle,
            PointsSpent = redemption.PointsSpent,
            State = redemption.State.ToString(),
            GrantReference = redemption.GrantReference,
            RejectionReason = redemption.RejectionReason,
            CatalogueVersion = redemption.CatalogueVersion,
            CreatedAt = redemption.CreatedAt,
            CompletedAt = redemption.CompletedAt
        };
    }
}
