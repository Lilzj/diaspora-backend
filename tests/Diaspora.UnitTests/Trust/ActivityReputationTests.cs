using System.Runtime.CompilerServices;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
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
using MediatR;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// XP for what the rest of the platform publishes (REP-14, D-3).
/// <para>
/// Guides, posts and comments paid; an event, a service, a job, a home, a listing, an approved
/// insight and confirmed volunteering paid nothing, though every module already announced them.
/// Each pays the member its event names, once per entity, and a removal that names the entity takes
/// it back. None of it is reward points.
/// </para>
/// <para>
/// The harness keeps one profile across deliveries and answers <c>HasAwardAsync</c> and the
/// unreversed-by-source lookup from that profile's own events, which is what the repository sees
/// once a delivery has saved.
/// </para>
/// </summary>
public sealed class ActivityReputationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid MemberGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly UserId Member = UserId.From(MemberGuid);

    private static readonly Guid SomeoneElse = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid EntityId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // ── The rules ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReputationActions.EventPublished, ReputationDimension.contribution, 10, 1, true)]
    [InlineData(ReputationActions.ProviderPublished, ReputationDimension.contribution, 10, null, true)]
    [InlineData(ReputationActions.JobPublished, ReputationDimension.contribution, 5, 1, true)]
    [InlineData(ReputationActions.HousingPublished, ReputationDimension.contribution, 5, 1, true)]
    [InlineData(ReputationActions.ListingPublished, ReputationDimension.contribution, 5, 1, true)]
    [InlineData(ReputationActions.NeighborhoodInsightApproved, ReputationDimension.contribution, 15, null, false)]
    [InlineData(ReputationActions.VolunteerHoursConfirmed, ReputationDimension.contribution, 15, null, false)]
    [InlineData(ReputationActions.ProviderRecommendationReceived, ReputationDimension.trust, 2, null, false)]
    public void Each_activity_is_priced_on_the_scale_guides_and_posts_use(
        string action, ReputationDimension dimension, int points, int? cooldownHours, bool isCountryScoped)
    {
        var rule = DefaultReputationRules.All.Should()
            .ContainSingle(item => item.ActionType == action).Subject;

        rule.Dimension.Should().Be(dimension);
        rule.Points.Should().Be(points);
        rule.CooldownHours.Should().Be(cooldownHours);
        rule.IsCountryScoped.Should().Be(isCountryScoped);
        rule.CreditPoints.Should().BeNull("publishing earns XP alone (D-2)");
        rule.IsAwarded.Should().BeTrue();
        rule.Label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_new_activities_arrived_in_rules_version_5()
    {
        DefaultReputationRules.Version.Should().BeGreaterThanOrEqualTo(5);
    }

    // ── Paying ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReputationActions.EventPublished, 10)]
    [InlineData(ReputationActions.ProviderPublished, 10)]
    [InlineData(ReputationActions.JobPublished, 5)]
    [InlineData(ReputationActions.HousingPublished, 5)]
    [InlineData(ReputationActions.ListingPublished, 5)]
    [InlineData(ReputationActions.NeighborhoodInsightApproved, 15)]
    [InlineData(ReputationActions.VolunteerHoursConfirmed, 15)]
    [InlineData(ReputationActions.ProviderRecommendationReceived, 2)]
    public async Task An_activity_pays_the_member_it_names_once_however_often_it_is_delivered(
        string action, int points)
    {
        var harness = new Harness();

        await harness.DeliverAsync(action, EntityId);
        await harness.DeliverAsync(action, EntityId);

        var recorded = harness.Profile.Events.Should().ContainSingle().Subject;
        recorded.ActionType.Should().Be(action);
        recorded.Points.Should().Be(points);
        recorded.AwardKey.Should().NotBeNullOrWhiteSpace();
        await harness.UnitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ReputationActions.EventPublished)]
    [InlineData(ReputationActions.ProviderPublished)]
    [InlineData(ReputationActions.JobPublished)]
    [InlineData(ReputationActions.HousingPublished)]
    [InlineData(ReputationActions.ListingPublished)]
    public async Task A_publication_lands_on_its_country(string action)
    {
        var harness = new Harness();

        await harness.DeliverAsync(action, EntityId);

        harness.Profile.Events.Should().ContainSingle()
            .Which.CountryCode.Should().Be(Harness.CountryCode);
    }

    /// <summary>
    /// Republishing an unpublished service is the same service. Its key is the provider, so it pays
    /// once however many times it goes back up.
    /// </summary>
    [Fact]
    public async Task Two_services_pay_twice_and_one_service_republished_pays_once()
    {
        var harness = new Harness();

        await harness.DeliverAsync(ReputationActions.ProviderPublished, EntityId);
        await harness.DeliverAsync(ReputationActions.ProviderPublished, EntityId);
        await harness.DeliverAsync(ReputationActions.ProviderPublished, Guid.NewGuid());

        harness.Profile.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task Publishing_pays_no_reward_points()
    {
        var harness = new Harness();

        foreach (var action in Harness.Actions)
        {
            await harness.DeliverAsync(action, Guid.NewGuid());
        }

        await harness.Events.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    // ── Who is not paid ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A job, a home or a listing can be published by a business that no member stands behind. It is
    /// skipped rather than paid to nobody.
    /// </summary>
    [Theory]
    [InlineData(ReputationActions.JobPublished)]
    [InlineData(ReputationActions.HousingPublished)]
    [InlineData(ReputationActions.ListingPublished)]
    [InlineData(ReputationActions.ProviderRecommendationReceived)]
    public async Task An_activity_that_names_no_member_pays_nobody(string action)
    {
        var harness = new Harness();

        await harness.DeliverAsync(action, EntityId, namesNobody: true);

        harness.Profile.Events.Should().BeEmpty();
        await harness.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task A_recommendation_pays_the_owner_and_never_the_recommender()
    {
        var harness = new Harness();

        await harness.DeliverAsync(
            ReputationActions.ProviderRecommendationReceived, EntityId, recommender: SomeoneElse);

        harness.Profile.Events.Should().ContainSingle()
            .Which.Dimension.Should().Be(ReputationDimension.trust);
    }

    [Fact]
    public async Task An_owner_recommending_their_own_service_earns_nothing()
    {
        var harness = new Harness();

        await harness.DeliverAsync(
            ReputationActions.ProviderRecommendationReceived, EntityId, recommender: MemberGuid);

        harness.Profile.Events.Should().BeEmpty();
    }

    // ── Taking it back ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ReputationActions.EventPublished, EventCancelledReversalConsumer.Reason)]
    [InlineData(ReputationActions.HousingPublished, HousingDeletedReversalConsumer.Reason)]
    [InlineData(ReputationActions.ListingPublished, ListingDeletedReversalConsumer.Reason)]
    public async Task Removing_what_was_published_takes_its_XP_back(string action, string reason)
    {
        var harness = new Harness();
        await harness.DeliverAsync(action, EntityId);
        var original = harness.Profile.Events.Single();

        await harness.RemoveAsync(action, EntityId);

        harness.Profile.Events.Should().HaveCount(2);
        var reversal = harness.Profile.Events.Single(item => item.IsReversal);
        reversal.ReversesEventId.Should().Be(original.Id);
        reversal.Description.Should().Be(reason);
        harness.Profile.Events.Sum(item => item.Points).Should().Be(0);
    }

    [Fact]
    public async Task A_removal_redelivered_takes_back_once()
    {
        var harness = new Harness();
        await harness.DeliverAsync(ReputationActions.ListingPublished, EntityId);

        await harness.RemoveAsync(ReputationActions.ListingPublished, EntityId);
        await harness.RemoveAsync(ReputationActions.ListingPublished, EntityId);

        harness.Profile.Events.Count(item => item.IsReversal).Should().Be(1);
    }

    [Fact]
    public async Task Removing_something_that_earned_nothing_saves_nothing()
    {
        var harness = new Harness();

        await harness.RemoveAsync(ReputationActions.EventPublished, EntityId);

        harness.Profile.Events.Should().BeEmpty();
        await harness.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    // ── Harness ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A contract with only the fields a consumer reads set. These events carry thirty or forty
    /// required members, none of which Trust reads; <c>with</c> on an uninitialized instance sets the
    /// few it does, as <c>AttributeEnrichmentTests</c> builds its contracts.
    /// </summary>
    private static T Blank<T>()
        where T : IIntegrationEvent =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private sealed class Harness
    {
        public const string CountryCode = "NG";

        public static readonly IReadOnlyList<string> Actions =
        [
            ReputationActions.EventPublished,
            ReputationActions.ProviderPublished,
            ReputationActions.JobPublished,
            ReputationActions.HousingPublished,
            ReputationActions.ListingPublished,
            ReputationActions.NeighborhoodInsightApproved,
            ReputationActions.VolunteerHoursConfirmed,
            ReputationActions.ProviderRecommendationReceived
        ];

        public Harness()
        {
            Profile = ReputationProfile.Open(Member, Now);
            var trust = TrustProfile.Open(Member, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            var reputations = Substitute.For<IReputationProfileRepository>();
            reputations.GetByUserAsync(Member, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations.GetByIdAsync(Profile.Id, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations
                .HasAwardAsync(Member, Arg.Any<ReputationAwardKey>(), Arg.Any<CancellationToken>())
                .Returns(call => Profile.Events.Any(
                    recorded => recorded.AwardKey == call.Arg<ReputationAwardKey>().Value));
            reputations
                .ListUnreversedBySourceAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Unreversed(item =>
                    item.SourceId == call.ArgAt<string>(1)
                    && (call.ArgAt<string?>(0) is not { } type || item.SourceType == type)));

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Member, Arg.Any<CancellationToken>()).Returns(trust);

            Events = Substitute.For<IIntegrationEventPublisher>();
            UnitOfWork = Substitute.For<IUnitOfWork>();

            Service = new ReputationService(reputations, profiles, new ReputationRuleCatalog([]), Events, clock);
            var provisioner = new TrustProfileProvisioner(
                profiles, reputations, Substitute.For<ITrustEventRepository>(), clock);
            Scorer = new ActivityReputationScorer(Service, provisioner, UnitOfWork);
        }

        public ReputationProfile Profile { get; }

        public ReputationService Service { get; }

        public ActivityReputationScorer Scorer { get; }

        public IIntegrationEventPublisher Events { get; }

        public IUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// Delivers the event that pays <paramref name="action"/> for one entity, naming the member
        /// unless <paramref name="namesNobody"/>.
        /// </summary>
        public Task DeliverAsync(
            string action, Guid entityId, bool namesNobody = false, Guid? recommender = null)
        {
            Guid? named = namesNobody ? null : MemberGuid;

            return action switch
            {
                ReputationActions.EventPublished => Deliver(
                    new EventPublishedReputationConsumer(Scorer),
                    Blank<EventPublishedIntegrationEvent>() with
                    {
                        CommunityEventId = entityId,
                        OrganizerUserId = named ?? Guid.Empty,
                        CountryCode = CountryCode
                    }),
                ReputationActions.ProviderPublished => Deliver(
                    new ProviderPublishedReputationConsumer(Scorer),
                    Blank<ProviderPublishedIntegrationEvent>() with
                    {
                        ProviderId = entityId,
                        OwnerId = named ?? Guid.Empty,
                        CountryCode = CountryCode
                    }),
                ReputationActions.JobPublished => Deliver(
                    new JobPublishedReputationConsumer(Scorer),
                    Blank<JobPublishedIntegrationEvent>() with
                    {
                        JobId = entityId,
                        EmployerUserId = named,
                        CountryCode = CountryCode
                    }),
                ReputationActions.HousingPublished => Deliver(
                    new HousingPublishedReputationConsumer(Scorer),
                    Blank<HousingPublishedIntegrationEvent>() with
                    {
                        ListingId = entityId,
                        LandlordUserId = named,
                        CountryCode = CountryCode
                    }),
                ReputationActions.ListingPublished => Deliver(
                    new ListingPublishedReputationConsumer(Scorer),
                    Blank<ListingPublishedIntegrationEvent>() with
                    {
                        ListingId = entityId,
                        SellerUserId = named,
                        CountryCode = CountryCode
                    }),
                ReputationActions.NeighborhoodInsightApproved => Deliver(
                    new NeighborhoodInsightApprovedReputationConsumer(Scorer),
                    Blank<NeighborhoodInsightApprovedIntegrationEvent>() with
                    {
                        InsightId = entityId,
                        AuthorId = named ?? Guid.Empty
                    }),
                ReputationActions.VolunteerHoursConfirmed => Deliver(
                    new VolunteerHoursConfirmedReputationConsumer(Scorer),
                    Blank<VolunteerHoursConfirmedIntegrationEvent>() with
                    {
                        AssignmentId = entityId,
                        VolunteerUserId = named ?? Guid.Empty
                    }),
                ReputationActions.ProviderRecommendationReceived => Deliver(
                    new ProviderRecommendationReputationConsumer(Scorer),
                    Blank<ProviderRecommendationReceivedIntegrationEvent>() with
                    {
                        RecommendationId = entityId,
                        ProviderId = Guid.NewGuid(),
                        RecommenderId = recommender ?? SomeoneElse,
                        OwnerUserId = named
                    }),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        /// <summary>Delivers the event that removes what <paramref name="action"/> was paid for.</summary>
        public Task RemoveAsync(string action, Guid entityId) => action switch
        {
            ReputationActions.EventPublished => Deliver(
                new EventCancelledReversalConsumer(Service, UnitOfWork),
                Blank<EventCancelledIntegrationEvent>() with { CommunityEventId = entityId }),
            ReputationActions.HousingPublished => Deliver(
                new HousingDeletedReversalConsumer(Service, UnitOfWork),
                Blank<HousingDeletedIntegrationEvent>() with { ListingId = entityId }),
            ReputationActions.ListingPublished => Deliver(
                new ListingDeletedReversalConsumer(Service, UnitOfWork),
                Blank<ListingDeletedIntegrationEvent>() with { ListingId = entityId }),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        private IReadOnlyList<ReputationEvent> Unreversed(Func<ReputationEvent, bool> match) =>
            [.. Profile.Events.Where(item =>
                match(item)
                && !item.IsReversal
                && !Profile.Events.Any(reversal => reversal.ReversesEventId == item.Id))];

        private static Task Deliver<TEvent>(
            INotificationHandler<IntegrationEventNotification<TEvent>> consumer, TEvent integrationEvent)
            where TEvent : IIntegrationEvent =>
            consumer.Handle(
                new IntegrationEventNotification<TEvent>(integrationEvent), CancellationToken.None);
    }
}
