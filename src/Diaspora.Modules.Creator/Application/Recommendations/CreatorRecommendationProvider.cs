using System.Globalization;
using Diaspora.Contracts.Recommendations.V1.Creator;
using Diaspora.Modules.Creator.Application.Challenges;
using Diaspora.Modules.Creator.Application.Grants;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Application.Verification;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Recommendations;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Creator.Application.Recommendations;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-013. Recommendations for a creator, from the creator's own state.
//
// Not a second recommendation engine (§28): nothing here ranks content or predicts behaviour.
// Every suggestion is a fact about the caller that the module already serves somewhere else — a
// milestone they completed and did not claim, a challenge they are most of the way through, a grant
// they qualify for, a course they started, a profile field they left empty, a verification they
// can apply for — re-read through the same repositories, eligibility rules and mappers the owning
// routes use, so the rail cannot disagree with the screen it links to.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Composes a member's creator suggestions, and stages their dismissals.
/// <para>
/// Served at <c>GET /api/v1/recommendations/creator</c> by the Recommendations module, which owns
/// that URL space and reaches this class only through <see cref="ICreatorRecommendationProvider"/>.
/// A service rather than a handler, so the composition can sit behind a published seam.
/// </para>
/// <para>
/// <b>Read-only on the list path.</b> A member with no creator record gets the suggestions that do
/// not need one — grants they would qualify for, verification eligibility — and no record is
/// opened by asking.
/// </para>
/// </summary>
public sealed class CreatorRecommendationProvider : ICreatorRecommendationProvider
{
    /// <summary>How far through a challenge counts as "close to completion".</summary>
    internal const int ChallengeNearlyDonePercentage = 50;

    /// <summary>How many catalogue rows each source reads. Catalogues here are tens, not thousands.</summary>
    private const int CatalogueRead = 100;

    private readonly CreatorContext _context;
    private readonly ICreatorMilestoneRepository _milestones;
    private readonly ICreatorMilestoneProgressRepository _milestoneProgress;
    private readonly ICreatorChallengeRepository _challenges;
    private readonly ICreatorChallengeEnrollmentRepository _enrollments;
    private readonly ICommunityGrantRepository _grants;
    private readonly IGrantApplicationRepository _applications;
    private readonly IAcademyRepository _academy;
    private readonly ICreatorLearningProgressRepository _learning;
    private readonly ICreatorVerificationRepository _verifications;
    private readonly CreatorVerificationRequirements _verificationRequirements;
    private readonly ICreatorRecommendationDismissalRepository _dismissals;
    private readonly ICreatorProfileRepository _creators;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreatorRecommendationProvider(
        CreatorContext context,
        ICreatorMilestoneRepository milestones,
        ICreatorMilestoneProgressRepository milestoneProgress,
        ICreatorChallengeRepository challenges,
        ICreatorChallengeEnrollmentRepository enrollments,
        ICommunityGrantRepository grants,
        IGrantApplicationRepository applications,
        IAcademyRepository academy,
        ICreatorLearningProgressRepository learning,
        ICreatorVerificationRepository verifications,
        CreatorVerificationRequirements verificationRequirements,
        ICreatorRecommendationDismissalRepository dismissals,
        ICreatorProfileRepository creators,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _milestones = milestones;
        _milestoneProgress = milestoneProgress;
        _challenges = challenges;
        _enrollments = enrollments;
        _grants = grants;
        _applications = applications;
        _academy = academy;
        _learning = learning;
        _verifications = verifications;
        _verificationRequirements = verificationRequirements;
        _dismissals = dismissals;
        _creators = creators;
        _guids = guids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CreatorRecommendationItem>> ListAsync(
        Guid userId, int take, CancellationToken cancellationToken = default)
    {
        var bounded = Math.Clamp(take, 1, CreatorRecommendationLimits.MaxTake);
        var now = _clock.UtcNow;

        var creator = await _creators.FindByUserAsync(userId, cancellationToken);

        var candidates = new List<CreatorRecommendationResponse>();

        if (creator is not null)
        {
            candidates.AddRange(await MilestonesAsync(creator, cancellationToken));
            candidates.AddRange(await ChallengesAsync(creator, now, cancellationToken));
            candidates.AddRange(await CoursesAsync(creator, cancellationToken));
            candidates.AddRange(ProfileGaps(creator));
        }

        candidates.AddRange(await GrantsAsync(creator, now, cancellationToken));
        candidates.AddRange(await VerificationAsync(creator, userId, cancellationToken));

        var dismissed = (await _dismissals.ListIdsForUserAsync(userId, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        // Ordered here and only here. Confidence first; then the expiring sooner; then the key, so
        // two equally confident suggestions never swap places between two requests.
        return candidates
            .Where(candidate => !dismissed.Contains(candidate.RecommendationId))
            .Where(candidate => candidate.ExpiresAt is null || candidate.ExpiresAt > now)
            .GroupBy(candidate => candidate.RecommendationId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.Confidence ?? 0)
            .ThenBy(candidate => candidate.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(candidate => candidate.RecommendationId, StringComparer.Ordinal)
            .Take(bounded)
            .Select(candidate => new CreatorRecommendationItem(
                candidate.RecommendationId,
                candidate.Kind,
                candidate.Title,
                candidate.Body,
                candidate.Reason,
                candidate.Icon,
                candidate.ActionLabel,
                candidate.ActionRoute,
                candidate.Confidence,
                candidate.ExpiresAt))
            .ToList();
    }

    /// <summary>
    /// Stages a dismissal. Idempotent, and any well-formed key is accepted: a suggestion can be
    /// dismissed while the list that showed it is being recomputed, and a repeat is the same
    /// dismissal. The unique index is the barrier against two taps racing.
    /// </summary>
    public async Task<DateTimeOffset> StageDismissalAsync(
        Guid userId, string recommendationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendationId);

        var key = recommendationId.Trim();
        var now = _clock.UtcNow;

        if (!await _dismissals.ExistsAsync(userId, key, cancellationToken))
        {
            _dismissals.Add(CreatorRecommendationDismissal.Record(
                CreatorRecommendationDismissalId.From(_guids.NewSequentialGuid()),
                userId,
                key,
                now));
        }

        return now;
    }

    /// <summary>Completed and unclaimed — the clearest thing a creator can do next.</summary>
    private async Task<IEnumerable<CreatorRecommendationResponse>> MilestonesAsync(
        CreatorProfile creator, CancellationToken cancellationToken)
    {
        var definitions = await _milestones.ListActiveAsync(cancellationToken);
        var rows = (await _milestoneProgress.ListForCreatorAsync(creator.Id, cancellationToken))
            .ToDictionary(row => row.MilestoneId);

        return definitions
            .Select(definition => definition.ToResponse(
                rows.TryGetValue(definition.Id, out var row) ? row : null))
            .Where(milestone => milestone.State == nameof(CreatorMilestoneState.Completed))
            .Select(milestone => new CreatorRecommendationResponse
            {
                RecommendationId = RecommendationKeys.Milestone(milestone.MilestoneId),
                Kind = "improvement",
                Title = "You have a milestone waiting",
                Body = milestone.Reward is { Label: { Length: > 0 } reward }
                    ? $"“{milestone.Title}” is complete and unclaimed. It earns {reward}."
                    : $"“{milestone.Title}” is complete and unclaimed.",
                Reason = milestone.CompletedAt is { } completed
                    ? $"You completed this on {Day(completed)} and have not claimed it."
                    : "You have completed this and not claimed it.",
                Icon = milestone.Icon,
                ActionLabel = "Claim it",
                ActionRoute = "/creator/milestones",
                Confidence = 1.0,
                ExpiresAt = milestone.ExpiresAt
            });
    }

    /// <summary>
    /// Joined challenges at least half done, and finished ones whose reward is unclaimed.
    /// </summary>
    private async Task<IEnumerable<CreatorRecommendationResponse>> ChallengesAsync(
        CreatorProfile creator, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var enrolments = (await _enrollments.ListForCreatorAsync(creator.Id, cancellationToken))
            .ToDictionary(enrollment => enrollment.ChallengeId);

        if (enrolments.Count == 0)
        {
            return [];
        }

        var challenges = await _challenges.ListPublishedAsync(CatalogueRead, cancellationToken);
        var result = new List<CreatorRecommendationResponse>();

        foreach (var challenge in challenges)
        {
            if (!enrolments.TryGetValue(challenge.Id, out var enrollment))
            {
                continue;
            }

            var (eligible, reason) = ChallengeEligibility.Evaluate(challenge, creator, enrollment, now);
            var response = challenge.ToResponse(enrollment, eligible, reason, now);
            var percentage = response.Progress.Percentage;

            if (response.State == nameof(CreatorChallengeState.Completed))
            {
                result.Add(new CreatorRecommendationResponse
                {
                    RecommendationId = RecommendationKeys.ChallengeReward(response.ChallengeId),
                    Kind = "challenge",
                    Title = "Claim your challenge reward",
                    Body = response.Reward is { Label: { Length: > 0 } reward }
                        ? $"You finished “{response.Title}”. Its reward is {reward}."
                        : $"You finished “{response.Title}”.",
                    Reason = "You completed the challenge and have not claimed the reward.",
                    Icon = response.Icon,
                    ActionLabel = "Claim it",
                    ActionRoute = $"/creator/challenges/{response.ChallengeId}",
                    Confidence = 0.95,
                    ExpiresAt = response.EndsAt
                });

                continue;
            }

            if (response.State is not (nameof(CreatorChallengeState.Joined)
                    or nameof(CreatorChallengeState.InProgress))
                || percentage < ChallengeNearlyDonePercentage
                || percentage >= 100)
            {
                continue;
            }

            var remaining = response.Tasks.Count(task => !task.IsComplete);

            result.Add(new CreatorRecommendationResponse
            {
                RecommendationId = RecommendationKeys.Challenge(response.ChallengeId),
                Kind = "challenge",
                Title = remaining == 1
                    ? "One task from finishing a challenge"
                    : "Close to finishing a challenge",
                Body = remaining > 0
                    ? $"“{response.Title}” has {Count(remaining, "task")} left."
                    : $"“{response.Title}” is {percentage}% done.",
                Reason = response.EndsAt is { } ends
                    ? $"You are {percentage}% through and the challenge ends on {Day(ends)}."
                    : $"You are {percentage}% through.",
                Icon = response.Icon,
                ActionLabel = "See the challenge",
                ActionRoute = $"/creator/challenges/{response.ChallengeId}",

                // 0.5 to 0.9 with progress: the closer, the more worth a nudge.
                Confidence = Math.Round(0.5 + (percentage / 100d * 0.4), 2),
                ExpiresAt = response.EndsAt
            });
        }

        return result;
    }

    /// <summary>Courses started and not finished.</summary>
    private async Task<IEnumerable<CreatorRecommendationResponse>> CoursesAsync(
        CreatorProfile creator, CancellationToken cancellationToken)
    {
        var started = (await _learning.ListForCreatorAsync(creator.Id, cancellationToken))
            .Where(row => row.State == CourseProgressState.InProgress)
            .ToDictionary(row => row.CourseId);

        if (started.Count == 0)
        {
            return [];
        }

        var courses = await _academy.ListCoursesAsync(sectionKey: null, level: null, cancellationToken);
        var result = new List<CreatorRecommendationResponse>();

        foreach (var course in courses)
        {
            if (!started.TryGetValue(course.Id, out var row))
            {
                continue;
            }

            var lessons = (await _academy.ListLessonsAsync(course.Id, cancellationToken))
                .Select(lesson => lesson.Id)
                .ToList();

            var response = course.ToResponse(row, lessons);

            if (response.Progress is not { } progress)
            {
                continue;
            }

            var left = progress.LessonCount - progress.LessonsCompleted;

            result.Add(new CreatorRecommendationResponse
            {
                RecommendationId = RecommendationKeys.Course(response.CourseId),
                Kind = "course",
                Title = $"Continue “{response.Title}”",
                Body = left > 0
                    ? $"{Count(left, "lesson")} left of {progress.LessonCount}."
                    : $"You are {progress.Percentage}% through.",
                Reason = progress.StartedAt is { } startedAt
                    ? $"You started this on {Day(startedAt)} and have not finished it."
                    : "You started this and have not finished it.",
                Icon = response.Icon,
                ActionLabel = "Continue",
                ActionRoute = $"/creator/academy/course/{response.CourseId}",
                Confidence = 0.8
            });
        }

        return result;
    }

    /// <summary>
    /// The profile fields <see cref="CreatorStatusPolicy.Completeness"/> reports missing, one each.
    /// </summary>
    private static IEnumerable<CreatorRecommendationResponse> ProfileGaps(CreatorProfile creator)
    {
        var (percentage, missing) = CreatorStatusPolicy.Completeness(creator);

        return missing.Select(requirement => new CreatorRecommendationResponse
        {
            RecommendationId = RecommendationKeys.Profile(requirement.Key),
            Kind = "improvement",
            Title = requirement.Label,
            Body = $"Your creator profile is {percentage}% complete.",
            Reason = "This is one of the things your profile is missing.",
            ActionLabel = requirement.ActionLabel ?? "Edit profile",
            ActionRoute = requirement.ActionRoute ?? "/creator/profile",
            Confidence = 0.6
        });
    }

    /// <summary>Open grants the caller qualifies for and has not applied to.</summary>
    private async Task<IEnumerable<CreatorRecommendationResponse>> GrantsAsync(
        CreatorProfile? creator, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var grants = await _grants.ListAsync(
            category: null, countryCode: null, CatalogueRead, cancellationToken);

        var mine = creator is null
            ? []
            : (await _applications.ListForCreatorAsync(creator.Id, cancellationToken))
                .Select(application => application.GrantId)
                .ToHashSet();

        var result = new List<CreatorRecommendationResponse>();

        foreach (var grant in grants)
        {
            if (mine.Contains(grant.Id))
            {
                continue;
            }

            var state = grant.StateAt(now);

            if (state is not (CommunityGrantState.Open or CommunityGrantState.ClosingSoon))
            {
                continue;
            }

            var (eligible, _, requirements) = GrantEligibility.Evaluate(grant, creator, now);

            if (!eligible)
            {
                continue;
            }

            var response = grant.ToResponse(null, requirements, eligible, null, now);
            var funding = response.FundingMaximum ?? response.FundingAmount;

            result.Add(new CreatorRecommendationResponse
            {
                RecommendationId = RecommendationKeys.Grant(response.GrantId),
                Kind = "grant",
                Title = $"You are eligible for “{response.Title}”",
                Body = response.DeadlineAt is { } deadline
                    ? $"{response.Summary} Up to {Money(funding)}. Closes on {Day(deadline)}."
                    : $"{response.Summary} Up to {Money(funding)}.",
                Reason = "You meet every requirement and have not applied.",
                ActionLabel = "Read the grant",
                ActionRoute = $"/creator/grants/{response.GrantId}",
                Confidence = state == CommunityGrantState.ClosingSoon ? 0.85 : 0.75,
                ExpiresAt = response.DeadlineAt
            });
        }

        return result;
    }

    /// <summary>
    /// Verification, when there is something the caller can do about it: apply, finish a draft, or
    /// answer a reviewer.
    /// </summary>
    private async Task<IEnumerable<CreatorRecommendationResponse>> VerificationAsync(
        CreatorProfile? creator, Guid userId, CancellationToken cancellationToken)
    {
        if (creator?.IsVerifiedCreator == true)
        {
            return [];
        }

        // The verification route's own reading: a transient profile for a member with no record, so
        // the requirements are spelled out the same way, and no row is written.
        var subject = creator ?? _context.TransientProfileFor(userId);
        var requirements = await _verificationRequirements.EvaluateAsync(subject, cancellationToken);

        var application = creator is null
            ? null
            : await _verifications.FindForCreatorAsync(creator.Id, cancellationToken);

        if (application is null)
        {
            if (!CreatorVerificationRequirements.AreMet(requirements))
            {
                return [];
            }

            return
            [
                new CreatorRecommendationResponse
                {
                    RecommendationId = RecommendationKeys.VerificationEligible,
                    Kind = "verification",
                    Title = "You can apply to be a verified creator",
                    Body = "Verified creators carry a badge and can apply for grants that ask for one.",
                    Reason = "You meet every requirement of the creator programme.",
                    ActionLabel = "Apply",
                    ActionRoute = "/creator/verification",
                    Confidence = 0.9
                }
            ];
        }

        var (title, body, reason, label) = application.State switch
        {
            CreatorVerificationState.Draft => (
                "Finish your verification application",
                "Your application is a draft and has not been submitted.",
                "You started an application and have not handed it in.",
                "Continue"),
            CreatorVerificationState.MoreInformationRequired => (
                "A reviewer needs more from you",
                "Your verification application is waiting for your answer.",
                application.RespondByAt is { } respondBy
                    ? $"The reviewer asked for more information. Reply by {Day(respondBy)}."
                    : "The reviewer asked for more information.",
                "Reply"),
            _ => (null, null, null, null)
        };

        if (title is null)
        {
            return [];
        }

        return
        [
            new CreatorRecommendationResponse
            {
                RecommendationId = RecommendationKeys.Verification(application.Id.Value, application.State),
                Kind = "verification",
                Title = title,
                Body = body!,
                Reason = reason!,
                ActionLabel = label!,
                ActionRoute = "/creator/verification",
                Confidence = 0.9,
                ExpiresAt = application.RespondByAt
            }
        ];
    }

    private static string Day(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Count(int count, string noun) =>
        count == 1 ? $"one {noun}" : $"{count.ToString(CultureInfo.InvariantCulture)} {noun}s";

    private static string Money(MoneyResponse money)
    {
        var amount = money.MinorUnits / (decimal)Math.Pow(10, money.Exponent);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{amount.ToString("#,##0.##", CultureInfo.InvariantCulture)} {money.Currency}");
    }
}

/// <summary>
/// The stable keys. One place, so the dismissal written today matches the suggestion served next
/// week.
/// </summary>
public static class RecommendationKeys
{
    public const string VerificationEligible = "verification:eligible";

    public static string Milestone(Guid milestoneId) => $"milestone:{milestoneId:D}";

    public static string Challenge(Guid challengeId) => $"challenge:{challengeId:D}";

    public static string ChallengeReward(Guid challengeId) => $"challenge-reward:{challengeId:D}";

    public static string Course(Guid courseId) => $"course:{courseId:D}";

    public static string Grant(Guid grantId) => $"grant:{grantId:D}";

    public static string Profile(string requirementKey) => $"profile:{requirementKey}";

    public static string Verification(Guid applicationId, CreatorVerificationState state) =>
        $"verification:{applicationId:D}:{state.ToString().ToLowerInvariant()}";
}
