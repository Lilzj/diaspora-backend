using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// A rule's daily cap (REP-09): at most N awards in any 24 hours, counted across countries, and
/// awards only, so a reversal does not hand a place back.
/// </summary>
public sealed class ReputationDailyCapTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Author = UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    [Fact]
    public void A_published_guide_is_capped_per_day_and_keeps_its_value()
    {
        var rule = DefaultReputationRules.All.Single(item => item.ActionType == ReputationActions.GuidePublished);

        rule.Points.Should().Be(50, "D-3: no value moves");
        rule.DailyCap.Should().Be(3);
        rule.CooldownHours.Should().BeNull("the cooldown is what refused a second distinct guide");
    }

    [Fact]
    public void Changing_the_cap_is_a_new_version_of_the_rules()
    {
        DefaultReputationRules.Version.Should().Be(7);
    }

    [Fact]
    public async Task An_award_inside_the_cap_is_paid()
    {
        var harness = new Harness(awardedInTheLastDay: 2);

        var outcome = await harness.ScoreGuideAsync();

        outcome.WasApplied.Should().BeTrue();
        outcome.Points.Should().Be(50);
    }

    [Fact]
    public async Task An_award_past_the_cap_is_skipped_and_says_why()
    {
        var harness = new Harness(awardedInTheLastDay: 3);

        var outcome = await harness.ScoreGuideAsync();

        outcome.WasApplied.Should().BeFalse();
        outcome.SkipReason.Should().Be("This action can only be scored 3 times a day.");
        harness.Profile.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task The_cap_asks_for_the_last_twenty_four_hours()
    {
        var harness = new Harness(awardedInTheLastDay: 0);

        await harness.ScoreGuideAsync();

        await harness.Reputations.Received(1).GetRuleUsageAsync(
            Author, ReputationActions.GuidePublished, "MU", Now.AddDays(-1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_rule_with_no_daily_cap_asks_for_no_window()
    {
        var harness = new Harness(awardedInTheLastDay: 0);

        await harness.Service.ScoreAsync(
            Author, ReputationActions.CommentWritten, ReputationAwardKey.Comment(Guid.NewGuid()));

        await harness.Reputations.Received(1).GetRuleUsageAsync(
            Author, ReputationActions.CommentWritten, null, null, Arg.Any<CancellationToken>());
    }

    private sealed class Harness
    {
        public Harness(int awardedInTheLastDay)
        {
            Profile = ReputationProfile.Open(Author, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            Reputations = Substitute.For<IReputationProfileRepository>();
            Reputations.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(Profile);
            Reputations
                .GetRuleUsageAsync(
                    Author,
                    Arg.Any<string>(),
                    Arg.Any<string?>(),
                    Arg.Any<DateTimeOffset?>(),
                    Arg.Any<CancellationToken>())
                .Returns(new RuleUsage(
                    awardedInTheLastDay, awardedInTheLastDay, Now.AddMinutes(-10), awardedInTheLastDay));

            Service = new ReputationService(
                Reputations,
                Substitute.For<ITrustProfileRepository>(),
                new ReputationRuleCatalog([]),
                Substitute.For<IIntegrationEventPublisher>(),
                clock);
        }

        public ReputationProfile Profile { get; }

        public IReputationProfileRepository Reputations { get; }

        public ReputationService Service { get; }

        public Task<ScoringOutcome> ScoreGuideAsync()
        {
            var guideId = Guid.NewGuid();

            return Service.ScoreAsync(
                Author,
                ReputationActions.GuidePublished,
                ReputationAwardKey.GuidePublished(guideId),
                "guide",
                guideId.ToString(),
                "MU");
        }
    }
}
