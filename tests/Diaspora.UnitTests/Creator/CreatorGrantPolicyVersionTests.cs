using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Creator.Application.EventHandlers;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// Every points grant Creator makes names the schedule that priced it (RWD-13, step 3e).
/// <para>
/// Trust's grants carry <c>reputation@{version}</c>; Creator's carried nothing, so "why did this
/// milestone pay 100" could not be answered once the row changed. A milestone and a challenge are
/// each the schedule for their own key, and a deployment's rows can differ from the seed code, so
/// each carries its own revision. The sign-up bonus is one configured number, so the number is the
/// version.
/// </para>
/// </summary>
public sealed class CreatorGrantPolicyVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_milestone_claim_names_the_revision_of_the_milestone_that_priced_it()
    {
        var definition = Milestone();

        definition.PolicyRevision.Should().Be(1);
        ClaimMilestone(definition).PolicyVersion.Should().Be("creator.milestone/first-guide@1");
    }

    [Fact]
    public void Redefining_a_milestone_moves_its_revision_and_the_next_claim_carries_it()
    {
        var definition = Milestone();

        definition.Redefine(
            definition.Title, definition.Description, definition.Target, definition.RewardLabel,
            250, definition.SortOrder, definition.Category, definition.Icon, definition.Unit,
            definition.ExpiresAt, definition.Rules.ToList());

        definition.PolicyRevision.Should().Be(2);
        ClaimMilestone(definition).PolicyVersion.Should().Be("creator.milestone/first-guide@2");
    }

    [Fact]
    public void A_challenge_claim_names_the_revision_of_the_challenge_that_priced_it()
    {
        var challenge = Challenge();

        challenge.PolicyRevision.Should().Be(1);
        ClaimChallenge(challenge).PolicyVersion.Should().Be("creator.challenge/sprint@1");
    }

    [Fact]
    public void Redefining_a_challenge_moves_its_revision_and_the_next_claim_carries_it()
    {
        var challenge = Challenge();

        challenge.Redefine(
            challenge.Title, challenge.Description, challenge.StartsAt, challenge.EndsAt,
            challenge.RewardLabel, 750, challenge.HasLeaderboard, challenge.Category, challenge.Icon,
            challenge.MinimumStatus, challenge.Tasks.ToList(), challenge.Rules.ToList(),
            challenge.Countries.ToList());

        challenge.PolicyRevision.Should().Be(2);
        ClaimChallenge(challenge).PolicyVersion.Should().Be("creator.challenge/sprint@2");
    }

    [Fact]
    public void The_signup_bonus_names_the_points_it_was_configured_to_pay()
    {
        var bonus = SignupBonus.Offer(
            SignupBonusId.New(), Guid.NewGuid(), CreatorId.New(), 100, Now, Now.AddDays(30));

        bonus.Acknowledge(true, "signup-bonus:x", null, Now).Should().BeTrue();

        bonus.DomainEvents.OfType<CreatorPointsEarnedDomainEvent>().Should().ContainSingle()
            .Which.PolicyVersion.Should().Be("creator.signup-bonus@100");
    }

    [Fact]
    public async Task The_relay_carries_the_version_onto_the_points_grant()
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var relay = new CreatorIntegrationEventRelay(publisher, NullLogger<CreatorIntegrationEventRelay>.Instance);

        await relay.PublishAsync(new CreatorPointsEarnedDomainEvent(
            CreatorId.New(), Guid.NewGuid(), 100, "Milestone:first-guide", "milestone:first-guide:x", Now,
            "creator.milestone/first-guide@3"));

        await publisher.Received(1).PublishAsync(
            Arg.Is<IIntegrationEvent>(published =>
                published is RewardPointsGrantedIntegrationEvent
                && ((RewardPointsGrantedIntegrationEvent)published).PolicyVersion == "creator.milestone/first-guide@3"),
            Arg.Any<CancellationToken>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────────────────────

    private static CreatorMilestoneDefinition Milestone() =>
        CreatorMilestoneDefinition
            .Define(CreatorMilestoneId.New(), "first-guide", "First guide", "…", 1, "points", 100, 1)
            .Counting(CreatorActivityTypes.GuidePublished, 1);

    private static CreatorPointsEarnedDomainEvent ClaimMilestone(CreatorMilestoneDefinition definition)
    {
        var progress = CreatorMilestoneProgress.Begin(
            CreatorMilestoneProgressId.New(), CreatorId.New(), Guid.NewGuid(), definition, Now);

        progress.RecordActivity(definition, CreatorActivityTypes.GuidePublished, 1, Now);
        progress.Claim(definition, "milestone:first-guide:x", null, Now).IsSuccess.Should().BeTrue();

        return progress.DomainEvents.OfType<CreatorPointsEarnedDomainEvent>().Should().ContainSingle().Subject;
    }

    private static CreatorChallenge Challenge() =>
        CreatorChallenge
            .Define(
                CreatorChallengeId.New(), "sprint", "Sprint", "…", Now.AddDays(-2), Now.AddDays(10),
                "points", 500, hasLeaderboard: false)
            .WithTask(CreatorChallengeTaskId.New(), "Publish a guide", CreatorActivityTypes.GuidePublished, target: 1)
            .Publish();

    private static CreatorPointsEarnedDomainEvent ClaimChallenge(CreatorChallenge challenge)
    {
        var enrollment = CreatorChallengeEnrollment.Join(
            CreatorChallengeEnrollmentId.New(), challenge, CreatorId.New(), Guid.NewGuid(), null, Now.AddHours(-1));

        enrollment.RecordActivity(challenge, CreatorActivityTypes.GuidePublished, 1, Now, Now);
        enrollment.Claim(challenge, "challenge:sprint:x", null, Now).IsSuccess.Should().BeTrue();

        return enrollment.DomainEvents.OfType<CreatorPointsEarnedDomainEvent>().Should().ContainSingle().Subject;
    }
}
