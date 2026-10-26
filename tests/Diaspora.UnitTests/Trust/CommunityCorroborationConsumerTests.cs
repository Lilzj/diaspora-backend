using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Domain.Abstractions;
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
/// The join between B6's corroboration count and this module's reputation.
/// <para>
/// Before this consumer the platform had two confirmation systems that could not see each other:
/// a <c>confirmed</c> reaction moved the count every card renders and paid the author nothing,
/// while <c>POST /trust/contributions/confirmations</c> paid the author and moved no count. The
/// client posted the second and displayed the first, so the number never moved.
/// </para>
/// </summary>
public sealed class CommunityCorroborationConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Author =
        UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static readonly UserId Confirmer =
        UserId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    private static readonly Guid PostId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>What <c>CommunityConfirmation</c> is worth in the default catalogue.</summary>
    private const int ConfirmationPoints = 5;

    [Fact]
    public async Task A_corroborating_reaction_pays_the_author()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Corroboration());

        // The point of the whole consumer: the reaction that moves B6's count now also moves
        // the author's standing, which no reaction ever did before.
        harness.AuthorReputation.TrustScore.Should().Be(ConfirmationPoints);
        harness.AuthorReputation.CommunityConfirmationsReceived.Should().Be(1);
        await harness.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_confirmation_is_recorded_against_the_content_not_the_reactor()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Corroboration());

        var recorded = harness.Recorded.Should().ContainSingle().Subject;

        recorded.ConfirmedByUserId.Should().Be(Confirmer);
        recorded.AuthorUserId.Should().Be(Author);
        recorded.TargetId.Should().Be(PostId.ToString());

        // The event's country, which is the content's community — not the reactor's, and not
        // the author's primary country, which is what the client used to send.
        recorded.CountryCode.Should().Be("MU");
    }

    /// <summary>
    /// Delivery is at-least-once and a member can withdraw a reaction and re-add it. Neither
    /// pays twice — <c>ScoreAsync</c> deduplicates on nothing, so the contribution row's unique
    /// index is the only thing between this consumer and a points faucet.
    /// </summary>
    [Fact]
    public async Task A_redelivery_or_a_re_reaction_pays_nothing_further()
    {
        var harness = new Harness { AlreadyConfirmed = true };

        await harness.DeliverAsync(Corroboration());

        harness.AuthorReputation.TrustScore.Should().Be(0);
        harness.Recorded.Should().BeEmpty();
        await harness.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Sentiment, not reaction key. A deployment that adds a second corroborating reaction to
    /// the catalogue pays the author without a release here; a helpful or a like never does.
    /// </summary>
    [Theory]
    [InlineData("positive")]
    [InlineData("negative")]
    [InlineData("neutral")]
    [InlineData("disputing")]
    public async Task Only_a_corroborating_sentiment_pays(string sentiment)
    {
        var harness = new Harness();

        await harness.DeliverAsync(
            Corroboration() with { Sentiment = sentiment, ReactionKey = "helpful" });

        harness.AuthorReputation.TrustScore.Should().Be(0);
        harness.Recorded.Should().BeEmpty();
    }

    [Fact]
    public async Task A_corroborating_sentiment_pays_whatever_its_reaction_key_is_called()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Corroboration() with { ReactionKey = "can-vouch-for-this" });

        harness.AuthorReputation.TrustScore.Should().Be(ConfirmationPoints);
    }

    /// <summary>
    /// Content another module owns travels on this contract with no author attached. There is
    /// nobody to pay, and the reactor is not a candidate.
    /// </summary>
    [Fact]
    public async Task Content_with_no_author_pays_nobody()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Corroboration() with { ContentAuthorId = null });

        harness.AuthorReputation.TrustScore.Should().Be(0);
        harness.Recorded.Should().BeEmpty();
    }

    /// <summary>
    /// B6 refuses self-corroboration at the point of reaction, so this should never arrive.
    /// Asserted anyway: a rule that pays points must not depend on another module keeping a
    /// guard this one cannot see.
    /// </summary>
    [Fact]
    public async Task Confirming_your_own_post_pays_nothing_even_if_B6_lets_it_through()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Corroboration() with { UserId = Author.Value });

        harness.AuthorReputation.TrustScore.Should().Be(0);
        harness.Recorded.Should().BeEmpty();
    }

    private static CommunityReactionAddedIntegrationEvent Corroboration() => new()
    {
        ContentType = "post",
        ContentId = PostId,
        UserId = Confirmer.Value,
        ReactionKey = "confirmed",
        Sentiment = "corroborating",
        ContentAuthorId = Author.Value,
        CountryCode = "MU"
    };

    private sealed class Harness
    {
        private readonly CommunityCorroborationConsumer _consumer;

        public Harness()
        {
            AuthorReputation = ReputationProfile.Open(Author, Now);
            AuthorTrust = TrustProfile.Open(Author, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            var contributions = Substitute.For<ICommunityContributionRepository>();

            contributions
                .ExistsAsync(
                    Arg.Any<UserId>(), Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => AlreadyConfirmed);

            contributions
                .When(repository => repository.AddAsync(
                    Arg.Any<CommunityContribution>(), Arg.Any<CancellationToken>()))
                .Do(call => Recorded.Add(call.Arg<CommunityContribution>()));

            var reputations = Substitute.For<IReputationProfileRepository>();
            reputations.GetByUserAsync(Author, Arg.Any<CancellationToken>())
                .Returns(AuthorReputation);

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(AuthorTrust);

            var timeline = Substitute.For<ITrustEventRepository>();

            UnitOfWork = Substitute.For<IUnitOfWork>();

            _consumer = new CommunityCorroborationConsumer(
                contributions,
                new ReputationService(reputations, profiles, new ReputationRuleCatalog([]), Substitute.For<IIntegrationEventPublisher>(), clock),
                new TrustProfileProvisioner(profiles, reputations, timeline, clock),
                UnitOfWork,
                clock);
        }

        public bool AlreadyConfirmed { get; init; }

        public List<CommunityContribution> Recorded { get; } = [];

        public ReputationProfile AuthorReputation { get; }

        public TrustProfile AuthorTrust { get; }

        public IUnitOfWork UnitOfWork { get; }

        public Task DeliverAsync(CommunityReactionAddedIntegrationEvent integrationEvent) =>
            _consumer.Handle(
                new IntegrationEventNotification<CommunityReactionAddedIntegrationEvent>(
                    integrationEvent),
                CancellationToken.None);
    }
}
