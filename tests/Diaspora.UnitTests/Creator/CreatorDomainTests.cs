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
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// The Creator Platform's rules, asserted where they are decided.
/// <para>
/// <b>Every test here is about a rule the register says a client must not own.</b> A weighted
/// milestone, a capped one, the status ladder, the challenge state groups, a quiz's grading and a
/// referral's four distinct states — each is a place where the frontend register's objection was
/// "the client would disagree with the server the day the rule changed", and each is asserted
/// against the server's own implementation rather than against a fixture.
/// </para>
/// </summary>
public sealed class CreatorProgressTests
{
    /// <summary>
    /// <b>The register's central arithmetic claim.</b> "Weighted and capped milestones make
    /// <c>current / target</c> wrong, and the fixture deliberately contains two that are." This is
    /// the server side of that: a featured guide counts three, so five ordinary guides and two
    /// featured ones is eleven rather than seven.
    /// </summary>
    [Fact]
    public void A_weighted_milestone_counts_a_featured_guide_three_times()
    {
        var definition = Weighted();
        var progress = Progress(definition);

        for (var index = 0; index < 5; index++)
        {
            progress.RecordActivity(
                definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);
        }

        progress.RecordActivity(
            definition, CreatorActivityTypes.GuideFeatured, 2, DateTimeOffset.UtcNow);

        progress.Current.Should().Be(
            11, "five ordinary guides at one plus two featured at three");

        progress.Progress().Percentage.Should().Be(
            73, "eleven of fifteen, rounded — and not five of fifteen, which a raw count would give");
    }

    /// <summary>
    /// <b>A cap is why a division is wrong even when the weights are one.</b> Twenty comments on a
    /// milestone that caps them at ten contribute ten, and the served percentage says so.
    /// </summary>
    [Fact]
    public void A_capped_rule_stops_contributing_once_it_reaches_its_cap()
    {
        var definition = Capped();
        var progress = Progress(definition);

        progress.RecordActivity(
            definition, CreatorActivityTypes.CommentAdded, 20, DateTimeOffset.UtcNow);

        progress.Current.Should().Be(10, "the cap bites at ten however many comments there are");
        progress.State.Should().Be(CreatorMilestoneState.InProgress);

        progress.RecordActivity(
            definition, CreatorActivityTypes.EventHosted, 2, DateTimeOffset.UtcNow);

        progress.Current.Should().Be(30, "ten capped comments plus two events at ten each");
        progress.State.Should().Be(CreatorMilestoneState.Completed);
    }

    /// <summary>A percentage is clamped at 100, however far past the target somebody is.</summary>
    [Fact]
    public void Progress_never_exceeds_one_hundred_percent()
    {
        CreatorProgress.Of(50, 10).Percentage.Should().Be(100);
        CreatorProgress.Of(50, 10).Current.Should().Be(50, "the raw figure is still the truth");
    }

    /// <summary>
    /// A target of zero is a configuration mistake rather than a completed milestone, and reads as
    /// 0% rather than 100%.
    /// </summary>
    [Fact]
    public void A_zero_target_reads_as_no_progress_rather_than_completion()
    {
        var figure = CreatorProgress.Of(5, 0);

        figure.Percentage.Should().Be(0);
        figure.IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// <b>Completion is raised once.</b> §8 of the phase brief: "The event must be emitted only once
    /// according to the platform's event semantics."
    /// </summary>
    [Fact]
    public void Completion_is_announced_once_and_not_again()
    {
        var definition = Simple(target: 2);
        var progress = Progress(definition);

        progress.RecordActivity(
                definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow)
            .Should().BeFalse("one of two is not complete");

        progress.RecordActivity(
                definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow)
            .Should().BeTrue("the second one crosses the target");

        progress.RecordActivity(
                definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow)
            .Should().BeFalse("a completed milestone does not complete again");
    }

    /// <summary>A claim is refused before completion, and refused a second time after it.</summary>
    [Fact]
    public void A_milestone_cannot_be_claimed_twice()
    {
        var definition = Simple(target: 1);
        var progress = Progress(definition);

        progress.Claim(definition, "ref", null, DateTimeOffset.UtcNow)
            .IsFailure.Should().BeTrue("nothing has been done yet");

        progress.RecordActivity(
            definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);

        progress.Claim(definition, "ref", null, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();

        var second = progress.Claim(definition, "ref", null, DateTimeOffset.UtcNow);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("creator.milestone.already-claimed");
    }

    /// <summary>
    /// A claim raises the points-earned event exactly once, and it carries a grant reference — which
    /// is the barrier B15 de-duplicates a redelivered grant on.
    /// </summary>
    [Fact]
    public void A_claim_raises_one_points_grant_carrying_a_reference()
    {
        var definition = Simple(target: 1);
        var progress = Progress(definition);

        progress.RecordActivity(
            definition, CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);

        progress.Claim(definition, "milestone:x:abc", null, DateTimeOffset.UtcNow);

        var grants = progress.DomainEvents
            .OfType<Modules.Creator.Domain.Events.CreatorPointsEarnedDomainEvent>()
            .ToList();

        grants.Should().ContainSingle();
        grants[0].GrantReference.Should().Be("milestone:x:abc");
        grants[0].Points.Should().Be(definition.RewardPoints);
    }

    private static CreatorMilestoneDefinition Simple(long target) =>
        CreatorMilestoneDefinition
            .Define(CreatorMilestoneId.New(), "simple", "Simple", "…", target, "points", 100, 1)
            .Counting(CreatorActivityTypes.GuidePublished, 1);

    private static CreatorMilestoneDefinition Weighted() =>
        CreatorMilestoneDefinition
            .Define(CreatorMilestoneId.New(), "weighted", "Weighted", "…", 15, "points", 100, 1)
            .Counting(CreatorActivityTypes.GuidePublished, 1)
            .Counting(CreatorActivityTypes.GuideFeatured, 3);

    private static CreatorMilestoneDefinition Capped() =>
        CreatorMilestoneDefinition
            .Define(CreatorMilestoneId.New(), "capped", "Capped", "…", 30, "points", 100, 1)
            .Counting(CreatorActivityTypes.EventHosted, 10)
            .Counting(CreatorActivityTypes.CommentAdded, 1, cap: 10);

    private static CreatorMilestoneProgress Progress(CreatorMilestoneDefinition definition) =>
        CreatorMilestoneProgress.Begin(
            CreatorMilestoneProgressId.New(),
            CreatorId.New(),
            Guid.NewGuid(),
            definition,
            DateTimeOffset.UtcNow);
}

/// <summary>The status ladder, and the three states that are not rungs on it.</summary>
public sealed class CreatorStatusPolicyTests
{
    [Fact]
    public void A_member_with_nothing_is_not_a_creator()
    {
        var (earned, served) = CreatorStatusPolicy.Resolve(NewCreator());

        earned.Should().Be(CreatorStatus.NotACreator);
        served.Should().Be(CreatorStatus.NotACreator);
    }

    [Fact]
    public void One_recognised_action_makes_a_contributor()
    {
        var creator = NewCreator();

        creator.RecordActivity(CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);

        CreatorStatusPolicy.Resolve(creator).Earned.Should().Be(CreatorStatus.Contributor);
    }

    /// <summary>
    /// <b>A suspended Top Creator is Suspended, and the rung they earned survives it.</b> Keeping
    /// the two apart is what makes lifting a suspension restore the standing rather than starting
    /// the climb again.
    /// </summary>
    [Fact]
    public void Suspension_overrides_the_rung_without_erasing_it()
    {
        var creator = Established();

        creator.ApplyStanding(isRestricted: false, isSuspended: true, "…", DateTimeOffset.UtcNow);

        var (earned, served) = CreatorStatusPolicy.Resolve(creator);

        served.Should().Be(CreatorStatus.Suspended);
        earned.Should().Be(
            CreatorStatus.EstablishedCreator, "the rung is what their work bought them");
    }

    /// <summary>
    /// <b>PendingVerification never demotes somebody who is already above it.</b> An Established
    /// Creator whose annual re-verification is with a reviewer keeps their standing while it is
    /// looked at.
    /// </summary>
    [Fact]
    public void A_review_in_flight_does_not_demote_an_established_creator()
    {
        var creator = Established();

        creator.ApplyVerificationStanding(isVerified: true, isInReview: true, DateTimeOffset.UtcNow);

        CreatorStatusPolicy.Resolve(creator).Served
            .Should().Be(CreatorStatus.EstablishedCreator);
    }

    [Fact]
    public void A_review_in_flight_does_show_on_somebody_below_verified()
    {
        var creator = NewCreator();

        creator.RecordActivity(CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);
        creator.ApplyVerificationStanding(isVerified: false, isInReview: true, DateTimeOffset.UtcNow);

        CreatorStatusPolicy.Resolve(creator).Served
            .Should().Be(CreatorStatus.PendingVerification);
    }

    /// <summary>
    /// The requirements for the next rung are served in words with their counters — which is
    /// CREATOR-GAP-001's central ask.
    /// </summary>
    [Fact]
    public void The_next_target_names_what_is_missing_with_its_numbers()
    {
        var creator = NewCreator();

        creator.RecordActivity(CreatorActivityTypes.GuidePublished, 1, DateTimeOffset.UtcNow);

        var target = CreatorStatusPolicy.NextTarget(creator);

        target.Should().NotBeNull();
        target!.Status.Should().Be(CreatorStatus.EmergingCreator);
        target.Requirements.Should().HaveCount(2);
        target.Requirements.Should().OnlyContain(requirement => !requirement.IsMet);
        target.Requirements[0].Target.Should().Be(5);
        target.Requirements[0].Current.Should().Be(1);
    }

    [Fact]
    public void A_suspended_creator_is_not_told_how_to_climb()
    {
        var creator = Established();

        creator.ApplyStanding(false, true, "…", DateTimeOffset.UtcNow);

        CreatorStatusPolicy.NextTarget(creator).Should().BeNull();
    }

    /// <summary>The status event fires on a real change and stays silent otherwise.</summary>
    [Fact]
    public void The_status_event_fires_only_when_the_served_status_moves()
    {
        var creator = NewCreator();

        creator.ApplyStatus(CreatorStatus.Contributor, CreatorStatus.Contributor, DateTimeOffset.UtcNow)
            .Should().BeTrue();

        creator.ClearDomainEvents();

        creator.ApplyStatus(CreatorStatus.Contributor, CreatorStatus.Contributor, DateTimeOffset.UtcNow)
            .Should().BeFalse("nothing the member can see has changed");

        creator.DomainEvents.Should().BeEmpty();
    }

    private static CreatorProfile NewCreator() =>
        CreatorProfile.Open(CreatorId.New(), Guid.NewGuid(), "Amara", "GB", DateTimeOffset.UtcNow);

    private static CreatorProfile Established()
    {
        var creator = NewCreator();

        for (var index = 0; index < 30; index++)
        {
            creator.RecordActivity(CreatorActivityTypes.GuidePublished, 10, DateTimeOffset.UtcNow);
        }

        creator.AdjustAudience(300, 0, 0, DateTimeOffset.UtcNow);
        creator.ApplyVerificationStanding(isVerified: true, isInReview: false, DateTimeOffset.UtcNow);

        var (earned, served) = CreatorStatusPolicy.Resolve(creator);
        creator.ApplyStatus(earned, served, DateTimeOffset.UtcNow);

        return creator;
    }
}

/// <summary>
/// The challenge state groups.
/// <para>
/// <b>The partition is the test.</b> §9 of the phase brief: "Verify that the state groups map
/// deterministically to the correct states." A challenge that appeared in two tabs or in none is the
/// failure a hand-written filter produces on its fourth state.
/// </para>
/// </summary>
public sealed class ChallengeStateResolverTests
{
    [Fact]
    public void The_three_groups_partition_every_state_exactly_once()
    {
        var all = Enum.GetValues<CreatorChallengeState>().ToList();

        var grouped = ChallengeStateResolver.Groups
            .SelectMany(group => group.Value)
            .ToList();

        grouped.Should().HaveCount(
            grouped.Distinct().Count(), "no state may appear in two groups");

        grouped.Should().BeEquivalentTo(all, "every state belongs to exactly one group");
    }

    [Theory]
    [InlineData("active")]
    [InlineData("OPEN")]
    [InlineData("finished")]
    public void A_group_name_parses_case_insensitively(string group) =>
        ChallengeStateResolver.Parse(group).Should().NotBeNull();

    [Fact]
    public void A_single_state_name_parses_to_itself() =>
        ChallengeStateResolver.Parse("Claimed")
            .Should().BeEquivalentTo(new[] { CreatorChallengeState.Claimed });

    /// <summary>
    /// An unrecognised value means "no filter" rather than "no results" — a stale client asking for
    /// a state this version does not have gets the unfiltered list rather than an empty screen.
    /// </summary>
    [Fact]
    public void An_unknown_state_is_no_filter_rather_than_an_empty_one() =>
        ChallengeStateResolver.Parse("nonsense").Should().BeNull();

    /// <summary>
    /// A member part-way through a challenge that has ended has failed it rather than being returned
    /// "expired", which reads as though the platform lost their attempt.
    /// </summary>
    [Fact]
    public void An_unfinished_enrolment_on_an_ended_challenge_reads_as_failed()
    {
        var challenge = Challenge(endsAt: DateTimeOffset.UtcNow.AddDays(-1));

        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.New(),
            challenge,
            CreatorId.New(),
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow.AddDays(-10));

        ChallengeStateResolver.Resolve(challenge, enrollment, DateTimeOffset.UtcNow)
            .Should().Be(CreatorChallengeState.Failed);
    }

    [Fact]
    public void A_cancelled_challenge_is_cancelled_for_everybody()
    {
        var challenge = Challenge(endsAt: DateTimeOffset.UtcNow.AddDays(10));
        challenge.Cancel();

        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.New(), challenge, CreatorId.New(), Guid.NewGuid(), null,
            DateTimeOffset.UtcNow);

        ChallengeStateResolver.Resolve(challenge, enrollment, DateTimeOffset.UtcNow)
            .Should().Be(CreatorChallengeState.Cancelled);
    }

    /// <summary>
    /// A challenge task counts only what happened after the member joined — the register's own
    /// objection to counting on the device: "wrong the moment a guide is unpublished".
    /// </summary>
    [Fact]
    public void A_task_ignores_activity_from_before_the_member_joined()
    {
        var challenge = Challenge(endsAt: DateTimeOffset.UtcNow.AddDays(10));
        var joined = DateTimeOffset.UtcNow;

        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.New(), challenge, CreatorId.New(), Guid.NewGuid(), null,
            joined);

        enrollment.RecordActivity(
            challenge,
            CreatorActivityTypes.GuidePublished,
            1,
            occurredAt: joined.AddDays(-5),
            now: DateTimeOffset.UtcNow);

        enrollment.OverallProgress(challenge).Current
            .Should().Be(0, "a guide published before joining does not count");

        enrollment.RecordActivity(
            challenge,
            CreatorActivityTypes.GuidePublished,
            1,
            occurredAt: joined.AddHours(1),
            now: DateTimeOffset.UtcNow);

        enrollment.OverallProgress(challenge).Current.Should().Be(1);
    }

    /// <summary>
    /// The overall figure caps each task at what it asked for, so somebody who did ten of a task
    /// that wanted three does not read as 300% complete. The leaderboard score is uncapped and is a
    /// different number on purpose.
    /// </summary>
    [Fact]
    public void Overall_progress_caps_each_task_but_the_score_does_not()
    {
        var challenge = Challenge(endsAt: DateTimeOffset.UtcNow.AddDays(10));

        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.New(), challenge, CreatorId.New(), Guid.NewGuid(), null,
            DateTimeOffset.UtcNow.AddHours(-1));

        enrollment.RecordActivity(
            challenge, CreatorActivityTypes.GuidePublished, 10, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var overall = enrollment.OverallProgress(challenge);

        overall.Current.Should().Be(3, "the task asked for three");
        overall.Percentage.Should().Be(100);
        enrollment.Score.Should().Be(10, "the ranking sees everything they did");
    }

    private static CreatorChallenge Challenge(DateTimeOffset endsAt) =>
        CreatorChallenge
            .Define(
                CreatorChallengeId.New(),
                "sprint",
                "Sprint",
                "…",
                DateTimeOffset.UtcNow.AddDays(-2),
                endsAt,
                "points",
                100,
                hasLeaderboard: true)
            .WithTask(
                CreatorChallengeTaskId.New(),
                "Publish three guides",
                CreatorActivityTypes.GuidePublished,
                target: 3)
            .Publish();
}

/// <summary>The two state machines, and the transitions they refuse.</summary>
public sealed class CreatorStateMachineTests
{
    [Theory]
    [InlineData(CreatorVerificationState.Draft, CreatorVerificationState.Submitted, true)]
    [InlineData(CreatorVerificationState.Draft, CreatorVerificationState.Approved, false)]
    [InlineData(CreatorVerificationState.Submitted, CreatorVerificationState.UnderReview, true)]
    [InlineData(CreatorVerificationState.UnderReview, CreatorVerificationState.Approved, true)]
    [InlineData(CreatorVerificationState.Approved, CreatorVerificationState.Rejected, false)]
    [InlineData(CreatorVerificationState.Rejected, CreatorVerificationState.AppealPending, true)]
    [InlineData(CreatorVerificationState.AppealPending, CreatorVerificationState.Approved, true)]
    [InlineData(CreatorVerificationState.Expired, CreatorVerificationState.Draft, true)]
    public void The_verification_machine_permits_exactly_what_the_register_draws(
        CreatorVerificationState from, CreatorVerificationState to, bool allowed) =>
        CreatorVerificationTransitions.IsAllowed(from, to).Should().Be(allowed);

    /// <summary>Suspension reaches every state and is the only thing that does.</summary>
    [Fact]
    public void Suspension_is_reachable_from_everywhere_but_itself()
    {
        foreach (var state in Enum.GetValues<CreatorVerificationState>())
        {
            CreatorVerificationTransitions
                .IsAllowed(state, CreatorVerificationState.Suspended)
                .Should().Be(state != CreatorVerificationState.Suspended);
        }
    }

    [Theory]
    [InlineData(GrantApplicationState.Draft, GrantApplicationState.Submitted, true)]
    [InlineData(GrantApplicationState.Draft, GrantApplicationState.Approved, false)]
    [InlineData(GrantApplicationState.Submitted, GrantApplicationState.Approved, true)]
    [InlineData(GrantApplicationState.Rejected, GrantApplicationState.Approved, false)]
    [InlineData(GrantApplicationState.Withdrawn, GrantApplicationState.Submitted, false)]
    [InlineData(GrantApplicationState.Approved, GrantApplicationState.Active, true)]
    [InlineData(GrantApplicationState.Active, GrantApplicationState.Completed, true)]
    public void The_grant_machine_refuses_what_it_should(
        GrantApplicationState from, GrantApplicationState to, bool allowed) =>
        GrantApplicationTransitions.IsAllowed(from, to).Should().Be(allowed);

    /// <summary>An appeal is permitted once, which is the single arrow out of Rejected.</summary>
    [Fact]
    public void An_application_may_be_appealed_once()
    {
        var application = Modules.Creator.Domain.Verification.CreatorVerificationApplication.Start(
            CreatorVerificationApplicationId.New(),
            CreatorId.New(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        application.Submit([], [], null, null, DateTimeOffset.UtcNow);
        application.BeginReview(Guid.NewGuid(), DateTimeOffset.UtcNow);
        application.Reject(Guid.NewGuid(), "Not yet.", DateTimeOffset.UtcNow);

        application.CanAppeal.Should().BeTrue();
        application.Appeal("Please look again.", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        application.Reject(Guid.NewGuid(), "Still no.", DateTimeOffset.UtcNow);

        application.CanAppeal.Should().BeFalse("one appeal per refusal");
        application.Appeal(null, DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
    }
}

/// <summary>
/// Grant form validation.
/// <para>
/// Every one of these is a rule §10 of the phase brief lists: required fields, field types, allowed
/// values, the declarations and the requested range.
/// </para>
/// </summary>
public sealed class GrantValidationTests
{
    [Fact]
    public void A_missing_required_answer_refuses_the_submission()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(grant, new Dictionary<string, string>(), null, null,
            ["declaration-1"], DateTimeOffset.UtcNow);

        var validated = application.Validate(grant);

        validated.IsFailure.Should().BeTrue();
        validated.Error.Code.Should().Be("creator.grant.field-required");
    }

    [Fact]
    public void A_select_answer_outside_the_served_options_is_refused()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(
            grant,
            new Dictionary<string, string> { ["name"] = "A project", ["kind"] = "not-an-option" },
            null,
            null,
            ["declaration-1"],
            DateTimeOffset.UtcNow);

        application.Validate(grant).Error.Code.Should().Be("creator.grant.field-invalid");
    }

    [Fact]
    public void A_number_field_refuses_text()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(
            grant,
            new Dictionary<string, string>
            {
                ["name"] = "A project", ["kind"] = "welcome", ["reach"] = "lots"
            },
            null,
            null,
            ["declaration-1"],
            DateTimeOffset.UtcNow);

        application.Validate(grant).Error.Code.Should().Be("creator.grant.field-invalid");
    }

    [Fact]
    public void An_unaccepted_declaration_refuses_the_submission()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(
            grant,
            new Dictionary<string, string> { ["name"] = "A project", ["kind"] = "welcome" },
            null,
            null,
            [],
            DateTimeOffset.UtcNow);

        application.Validate(grant).Error.Code.Should().Be("creator.grant.declaration-required");
    }

    /// <summary>A key the form does not define is dropped rather than stored.</summary>
    [Fact]
    public void An_answer_to_a_question_the_form_never_asked_is_dropped()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(
            grant,
            new Dictionary<string, string> { ["name"] = "A project", ["smuggled"] = "value" },
            null,
            null,
            ["declaration-1"],
            DateTimeOffset.UtcNow);

        application.Answers.Should().NotContain(answer => answer.FieldKey == "smuggled");
    }

    /// <summary>An approval above what the grant awards is refused, whoever asks.</summary>
    [Fact]
    public void An_award_above_the_grant_maximum_is_refused()
    {
        var grant = Grant();
        var application = Application(grant);

        application.SaveAnswers(
            grant,
            new Dictionary<string, string> { ["name"] = "A", ["kind"] = "welcome" },
            null,
            null,
            ["declaration-1"],
            DateTimeOffset.UtcNow);

        application.Submit(grant, null, DateTimeOffset.UtcNow);

        application.Approve(grant, Guid.NewGuid(), 999_999, null, DateTimeOffset.UtcNow)
            .Error.Code.Should().Be("creator.grant.amount-out-of-range");
    }

    private static CommunityGrant Grant() =>
        CommunityGrant
            .Define(
                CommunityGrantId.New(),
                "fund",
                "Fund",
                "…",
                "…",
                "community",
                fundingMinorUnits: 200_000,
                currency: "GBP")
            .WithField("name", "Name", GrantFieldKind.Text, isRequired: true)
            .WithField("kind", "Kind", GrantFieldKind.Select, isRequired: false,
                options: [("welcome", "Welcome"), ("legal", "Legal")])
            .WithField("reach", "Reach", GrantFieldKind.Number, isRequired: false)
            .WithDeclaration("Everything here is true.")
            .Publish();

    private static GrantApplication Application(CommunityGrant grant) =>
        GrantApplication.Start(
            GrantApplicationId.New(), grant.Id, CreatorId.New(), Guid.NewGuid(),
            DateTimeOffset.UtcNow);
}

/// <summary>
/// Quiz grading.
/// <para>
/// <b>The answer key is read here and returned nowhere.</b> The grading returns an outcome per
/// question, and the type it returns has no member that could carry a correct option.
/// </para>
/// </summary>
public sealed class AcademyQuizTests
{
    [Fact]
    public void A_correct_submission_passes_and_a_wrong_one_does_not()
    {
        var (quiz, correct, wrong) = Quiz();

        quiz.Grade(correct).Passed.Should().BeTrue();
        quiz.Grade(wrong).Passed.Should().BeFalse();
    }

    /// <summary>An unanswered question is wrong rather than skipped.</summary>
    [Fact]
    public void An_unanswered_question_counts_against_the_pass_mark()
    {
        var (quiz, correct, _) = Quiz();

        var partial = correct.Take(1).ToDictionary(pair => pair.Key, pair => pair.Value);

        var grading = quiz.Grade(partial);

        grading.Passed.Should().BeFalse();
        grading.CorrectCount.Should().Be(1);
        grading.TotalCount.Should().Be(2);
        grading.Outcomes.Should().HaveCount(2);
    }

    /// <summary>
    /// An answer naming an option that is not on the question is a malformed request rather than a
    /// wrong answer, so a client bug stays visible.
    /// </summary>
    [Fact]
    public void An_option_that_is_not_on_the_question_is_malformed_rather_than_wrong()
    {
        var (quiz, correct, _) = Quiz();

        var invented = correct.ToDictionary(pair => pair.Key, _ => Guid.NewGuid());

        quiz.Grade(invented).IsMalformed.Should().BeTrue();
    }

    /// <summary>
    /// <b>The projection carries no answer key.</b> The register: "<c>LessonQuizOption</c> carries
    /// no <c>isCorrect</c>. The answer key never reaches the device."
    /// </summary>
    [Fact]
    public void The_served_quiz_shape_has_no_member_that_could_carry_an_answer()
    {
        var properties = typeof(Modules.Creator.Contracts.V1.Responses.LessonQuizOptionResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().BeEquivalentTo(["OptionId", "Label"]);

        typeof(Modules.Creator.Contracts.V1.Responses.LessonQuizQuestionResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("Explanation",
                "an explanation before answering is half the answer");
    }

    /// <summary>A quiz lesson cannot be completed by assertion. That is the whole refusal.</summary>
    [Fact]
    public void A_quiz_lesson_refuses_an_asserted_completion()
    {
        var course = AcademyCourse.Define(
            AcademyCourseId.New(), "c", "C", "…", CourseLevel.Beginner, 5, 1);

        var lesson = AcademyLesson.Define(
            AcademyLessonId.New(), course.Id, "l", "L", LessonKind.Quiz, 5, 1);

        var progress = CreatorLearningProgress.Begin(
            CreatorLearningProgressId.New(), CreatorId.New(), Guid.NewGuid(), course,
            DateTimeOffset.UtcNow);

        var completed = progress.CompleteLesson(lesson, DateTimeOffset.UtcNow);

        completed.IsFailure.Should().BeTrue();
        completed.Error.Code.Should().Be("creator.lesson.quiz_required");
    }

    /// <summary>
    /// <b>Progress survives a revision.</b> CREATOR-GAP-006's versioning rule: a lesson rewritten
    /// from version 1 to 2 leaves the completion standing, and the read can say it was revised.
    /// </summary>
    [Fact]
    public void A_completion_survives_the_lesson_being_rewritten()
    {
        var course = AcademyCourse.Define(
            AcademyCourseId.New(), "c", "C", "…", CourseLevel.Beginner, 5, 1);

        var lesson = AcademyLesson.Define(
            AcademyLessonId.New(), course.Id, "l", "L", LessonKind.Reading, 5, 1);

        var progress = CreatorLearningProgress.Begin(
            CreatorLearningProgressId.New(), CreatorId.New(), Guid.NewGuid(), course,
            DateTimeOffset.UtcNow);

        progress.CompleteLesson(lesson, DateTimeOffset.UtcNow);

        progress.CompletedAtVersion(lesson.Id).Should().Be(1);

        lesson.Redefine("L", LessonKind.Reading, 5, 1, null, [], []);

        lesson.ContentVersion.Should().Be(2);
        progress.HasCompleted(lesson.Id).Should().BeTrue("the member did read it");
        progress.CompletedAtVersion(lesson.Id).Should().Be(1, "but they read version one");
    }

    private static (AcademyQuiz Quiz, Dictionary<Guid, Guid> Correct, Dictionary<Guid, Guid> Wrong)
        Quiz()
    {
        var lessonId = AcademyLessonId.New();
        var quiz = AcademyQuiz.Define(AcademyQuizId.New(), lessonId, "q", passMark: 2);

        var firstQuestion = AcademyQuizQuestionId.New();
        var firstRight = Guid.NewGuid();
        var firstWrong = Guid.NewGuid();

        var secondQuestion = AcademyQuizQuestionId.New();
        var secondRight = Guid.NewGuid();
        var secondWrong = Guid.NewGuid();

        quiz.WithQuestion(firstQuestion, "One?", "Because.",
            [(firstRight, "Right", true), (firstWrong, "Wrong", false)]);

        quiz.WithQuestion(secondQuestion, "Two?", null,
            [(secondRight, "Right", true), (secondWrong, "Wrong", false)]);

        return (
            quiz,
            new Dictionary<Guid, Guid>
            {
                [firstQuestion.Value] = firstRight,
                [secondQuestion.Value] = secondRight
            },
            new Dictionary<Guid, Guid>
            {
                [firstQuestion.Value] = firstWrong,
                [secondQuestion.Value] = secondWrong
            });
    }
}

/// <summary>
/// Referrals.
/// <para>
/// <b>The four states stay distinct.</b> The register: "a client counting signups and promising a
/// reward would be promising money the platform never agreed to pay."
/// </para>
/// </summary>
public sealed class CreatorReferralTests
{
    [Fact]
    public void A_signup_is_not_a_qualification()
    {
        var referral = Referral();

        referral.RecordSignup(Guid.NewGuid(), "Amara", DateTimeOffset.UtcNow);

        referral.State.Should().Be(ReferralState.SignedUp);
        referral.QualifiedAt.Should().BeNull();
        referral.RewardPoints.Should().Be(0);
    }

    [Fact]
    public void A_self_referral_is_refused()
    {
        var code = Code();
        var referral = CreatorReferral.Invite(
            CreatorReferralId.New(), code, DateTimeOffset.UtcNow);

        var recorded = referral.RecordSignup(code.UserId, "Me", DateTimeOffset.UtcNow);

        recorded.IsFailure.Should().BeTrue();
        recorded.Error.Code.Should().Be("creator.referral.self");
    }

    [Fact]
    public void A_referral_cannot_qualify_twice()
    {
        var referral = Referral();

        referral.RecordSignup(Guid.NewGuid(), "Amara", DateTimeOffset.UtcNow);

        referral.Qualify(250, "ref", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        referral.Qualify(250, "ref", DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();

        referral.DomainEvents
            .OfType<Modules.Creator.Domain.Events.ReferralQualifiedDomainEvent>()
            .Should().ContainSingle("a redelivered qualification pays once");
    }

    [Fact]
    public void A_referral_past_its_window_cannot_qualify()
    {
        var referral = CreatorReferral.Invite(
            CreatorReferralId.New(), Code(),
            DateTimeOffset.UtcNow.AddDays(-CreatorLimits.ReferralQualificationDays - 1));

        referral.RecordSignup(Guid.NewGuid(), "Amara", DateTimeOffset.UtcNow);

        referral.HasLapsed(DateTimeOffset.UtcNow).Should().BeTrue();
        referral.Qualify(250, "ref", DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// <b>A refusal names nothing.</b> A fraud refusal that explains itself is a map for getting
    /// round it.
    /// </summary>
    [Fact]
    public void A_declined_referral_carries_only_a_member_safe_sentence()
    {
        var referral = Referral();

        referral.RecordSignup(Guid.NewGuid(), "Amara", DateTimeOffset.UtcNow);
        referral.Reject(ReferralQualificationPolicy.DeclinedReason, DateTimeOffset.UtcNow);

        referral.StateReason.Should().Be("This referral did not qualify for a reward.");
        referral.StateReason.Should().NotContainAny("fraud", "risk", "check", "signal", "abuse");
    }

    private static CreatorReferralCode Code() =>
        CreatorReferralCode.Issue(
            CreatorReferralCodeId.New(), CreatorId.New(), Guid.NewGuid(), "ABCD2345",
            DateTimeOffset.UtcNow);

    private static CreatorReferral Referral() =>
        CreatorReferral.Invite(CreatorReferralId.New(), Code(), DateTimeOffset.UtcNow);
}

/// <summary>The sign-up bonus, and the silence its refusal keeps.</summary>
public sealed class SignupBonusTests
{
    [Fact]
    public void Acknowledging_with_requirements_outstanding_leaves_it_pending()
    {
        var bonus = Bonus();

        bonus.Acknowledge(requirementsMet: false, "ref", null, DateTimeOffset.UtcNow)
            .Should().BeFalse();

        bonus.State.Should().Be(SignupBonusState.Pending);
        bonus.AwardedAt.Should().BeNull();
    }

    [Fact]
    public void Acknowledging_with_everything_met_awards_once()
    {
        var bonus = Bonus();

        bonus.Acknowledge(true, "ref", "key", DateTimeOffset.UtcNow).Should().BeTrue();
        bonus.State.Should().Be(SignupBonusState.Awarded);

        bonus.Acknowledge(true, "ref", "key", DateTimeOffset.UtcNow)
            .Should().BeFalse("an awarded bonus stays awarded");

        bonus.DomainEvents
            .OfType<Modules.Creator.Domain.Events.SignupBonusAwardedDomainEvent>()
            .Should().ContainSingle();
    }

    /// <summary>
    /// <b>The blocked reason names no check.</b> Asserted here as well as on the client, because the
    /// client's assertion protects the client's copy and this one protects the platform's.
    /// </summary>
    [Fact]
    public void A_blocked_bonus_says_nothing_about_why()
    {
        var bonus = Bonus();

        bonus.Block("This bonus is not available on your account.", DateTimeOffset.UtcNow);

        bonus.State.Should().Be(SignupBonusState.Blocked);
        bonus.MemberSafeReason.Should().NotContainAny(
            "fraud", "risk", "velocity", "device", "duplicate", "flag", "score");
    }

    [Fact]
    public void An_expired_bonus_cannot_be_acknowledged_into_an_award()
    {
        var bonus = SignupBonus.Offer(
            SignupBonusId.New(),
            Guid.NewGuid(),
            CreatorId.New(),
            100,
            DateTimeOffset.UtcNow.AddDays(-40),
            DateTimeOffset.UtcNow.AddDays(-1));

        bonus.Acknowledge(true, "ref", null, DateTimeOffset.UtcNow).Should().BeFalse();
        bonus.State.Should().Be(SignupBonusState.Expired);
    }

    private static SignupBonus Bonus() =>
        SignupBonus.Offer(
            SignupBonusId.New(),
            Guid.NewGuid(),
            CreatorId.New(),
            100,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30));
}

/// <summary>The reward catalogue's stated answers.</summary>
public sealed class CreatorRewardCatalogueTests
{
    /// <summary>
    /// §14 of the phase brief asks for the expiry policy to be stated rather than left silent. The
    /// platform's answer is <c>Never</c>, and saying so is what makes it an answer.
    /// </summary>
    [Fact]
    public void The_points_expiry_policy_is_stated()
    {
        CreatorRewardCatalogueVersion.ExpiryPolicy.Should().Be(PointsExpiryPolicy.Never);
        CreatorRewardCatalogueVersion.ExpiryDescription.Should().NotBeNullOrWhiteSpace();
        CreatorRewardCatalogueVersion.Current.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A redemption's cost is the catalogue's, copied at the moment it happened — so a later price
    /// change does not rewrite what somebody paid.
    /// </summary>
    [Fact]
    public void A_redemption_records_the_price_it_was_charged()
    {
        var reward = CreatorRewardDefinition.Define(
            CreatorRewardId.New(), "slot", "Slot", "…", pointsCost: 1_500, sortOrder: 1);

        var redemption = CreatorRewardRedemption.Open(
            CreatorRewardRedemptionId.New(), reward, CreatorId.New(), Guid.NewGuid(),
            "debit-ref", "key", DateTimeOffset.UtcNow);

        reward.Redefine("Slot", "…", 9_999, 1, null, null, null,
            CreatorStatus.NotACreator, null, null, null, null);

        redemption.PointsSpent.Should().Be(1_500);
        redemption.CatalogueVersion.Should().Be(CreatorRewardCatalogueVersion.Current);
    }

    /// <summary>Every seeded activity type is one the module recognises.</summary>
    [Fact]
    public void The_recognised_action_vocabulary_is_closed()
    {
        CreatorActivityTypes.IsKnown(CreatorActivityTypes.GuidePublished).Should().BeTrue();
        CreatorActivityTypes.IsKnown("guide.viewed").Should().BeFalse(
            "a view is an audience measurement, and CREATOR-GAP-007 belongs to B19");
        CreatorActivityTypes.IsKnown(null).Should().BeFalse();
    }

    /// <summary>
    /// <b>Nothing in the recognised vocabulary is an audience measurement.</b> §21 of the phase
    /// brief forbids inventing "30-day views" or "engagement rate" from what this module holds, and
    /// the way to keep that true is for the vocabulary to contain no such thing to invent one from.
    /// </summary>
    [Fact]
    public void No_recognised_action_is_an_audience_measurement()
    {
        var forbidden = new[] { "view", "impression", "reach", "engagement", "click", "watch" };

        foreach (var action in CreatorActivityTypes.All)
        {
            action.Should().NotContainAny(forbidden);
        }
    }
}
