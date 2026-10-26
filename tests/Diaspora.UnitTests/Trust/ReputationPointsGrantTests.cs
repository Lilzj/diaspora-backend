using Diaspora.Contracts.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Trust.Application.Consumers;
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
/// Trust pays reward points for confirmed outcomes, and takes them back with the award (D-2, D-12).
/// <para>
/// Nothing paid points for content: the Financial Platform had a grant seam that only Creator's
/// milestones and challenges used. Trust now publishes a grant when an award whose rule has credit
/// points is applied, under the award's own key, and the matching reversal when the award is taken
/// back. Publishing earns XP alone.
/// </para>
/// </summary>
public sealed class ReputationPointsGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Author = UserId.From(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    [Fact]
    public async Task An_approved_guide_grants_its_points_under_the_award_key()
    {
        var harness = new Harness();
        var key = ReputationAwardKey.GuideApproved(Guid.NewGuid());

        await harness.Service.ScoreAsync(Author, ReputationActions.GuideApproved, key, "guide", "guide-1");

        harness.Published.OfType<RewardPointsGrantedIntegrationEvent>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                MemberUserId = Author.Value,
                Points = 250L,
                Source = ReputationActions.GuideApproved,
                GrantReference = key.Value,
                GrantedAt = Now,
                PolicyVersion = $"reputation@{DefaultReputationRules.Version}"
            });
    }

    [Fact]
    public async Task A_confirmed_update_grants_200_points()
    {
        var harness = new Harness();

        await harness.Service.ScoreAsync(
            Author, ReputationActions.UpdateVerified, ReputationAwardKey.UpdateVerified(Guid.NewGuid()));

        harness.Published.OfType<RewardPointsGrantedIntegrationEvent>().Should().ContainSingle()
            .Which.Points.Should().Be(200);
    }

    /// <summary>D-2: publishing anything earns XP alone.</summary>
    [Fact]
    public async Task Publishing_a_guide_grants_no_points()
    {
        var harness = new Harness();

        await harness.Service.ScoreAsync(
            Author, ReputationActions.GuidePublished, ReputationAwardKey.GuidePublished(Guid.NewGuid()));

        harness.Profile.ContributionScore.Should().Be(50);
        harness.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task A_redelivered_approval_grants_once()
    {
        var harness = new Harness();
        var key = ReputationAwardKey.GuideApproved(Guid.NewGuid());

        await harness.Service.ScoreAsync(Author, ReputationActions.GuideApproved, key);
        await harness.Service.ScoreAsync(Author, ReputationActions.GuideApproved, key);

        harness.Published.OfType<RewardPointsGrantedIntegrationEvent>().Should().ContainSingle();
    }

    /// <summary>CS-B1 reverses the XP; the points go with it, if they have not vested.</summary>
    [Fact]
    public async Task Taking_back_an_approved_guide_withdraws_its_grant()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();
        var key = ReputationAwardKey.GuideApproved(guideId);

        await harness.Service.ScoreAsync(
            Author, ReputationActions.GuideApproved, key, "guide", guideId.ToString());

        await harness.Service.ReverseBySourceAsync(
            null, guideId.ToString(), GuideDeletedReversalConsumer.Reason, ReversalScope.Awards);

        harness.Published.OfType<RewardPointsReversedIntegrationEvent>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                MemberUserId = Author.Value,
                GrantReference = key.Value,
                Reason = GuideDeletedReversalConsumer.Reason
            });
    }

    [Fact]
    public async Task Taking_back_an_award_that_paid_no_points_withdraws_nothing()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();

        await harness.Service.ScoreAsync(
            Author, ReputationActions.GuidePublished, ReputationAwardKey.GuidePublished(guideId),
            "guide", guideId.ToString());

        await harness.Service.ReverseBySourceAsync(
            null, guideId.ToString(), GuideDeletedReversalConsumer.Reason, ReversalScope.Awards);

        harness.Profile.ContributionScore.Should().Be(0);
        harness.Published.Should().BeEmpty();
    }

    private sealed class Harness
    {
        public Harness()
        {
            Profile = ReputationProfile.Open(Author, Now);
            var trust = TrustProfile.Open(Author, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            var reputations = Substitute.For<IReputationProfileRepository>();
            reputations.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations.GetByIdAsync(Profile.Id, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations
                .HasAwardAsync(Author, Arg.Any<ReputationAwardKey>(), Arg.Any<CancellationToken>())
                .Returns(call => Profile.Events.Any(
                    recorded => recorded.AwardKey == call.Arg<ReputationAwardKey>().Value));
            reputations
                .ListUnreversedBySourceAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => (IReadOnlyList<ReputationEvent>)[.. Profile.Events.Where(item =>
                    item.SourceId == call.ArgAt<string>(1)
                    && !item.IsReversal
                    && !Profile.Events.Any(reversal => reversal.ReversesEventId == item.Id))]);

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(trust);

            var events = Substitute.For<IIntegrationEventPublisher>();
            events
                .When(publisher => publisher.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>()))
                .Do(call => Published.Add(call.Arg<IIntegrationEvent>()));

            Service = new ReputationService(reputations, profiles, new ReputationRuleCatalog([]), events, clock);
        }

        public ReputationProfile Profile { get; }

        public ReputationService Service { get; }

        public List<IIntegrationEvent> Published { get; } = [];
    }
}
