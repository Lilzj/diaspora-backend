using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Consumers;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// One award per business fact.
/// <para>
/// Before awards were keyed, <c>ScoreAsync</c> deduplicated on a rule's cap and cooldown and on
/// nothing else, and most rules have neither. Each test here is a way that paid twice: a vote
/// changed and changed back, a guide re-verified, a new version of an old guide, and plain
/// redelivery — which the outbox does whenever any sibling handler of the same event throws.
/// </para>
/// <para>
/// The harness keeps one profile across deliveries and answers <c>HasAwardAsync</c> from that
/// profile's own events, which is what the repository sees once the first delivery has saved.
/// Cap and cooldown usage is left at zero throughout, so a test that passes here passes because
/// of the key and not because a cooldown happened to cover it.
/// </para>
/// </summary>
public sealed class ReputationAwardKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly UserId Author = UserId.From(AuthorGuid);

    private static readonly Guid VoterA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid VoterB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static readonly Guid GuideId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    // ── The key itself ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The historical backfill writes these keys in SQL, so their exact shape is a contract with
    /// <c>docs/migrations/2026-10-21-reputation-award-keys-backfill.sql</c>.
    /// </summary>
    [Fact]
    public void Keys_use_lowercase_unhyphenated_ids_as_the_backfill_does()
    {
        ReputationAwardKey.GuidePublished(GuideId).Value
            .Should().Be("GuidePublished:guide:cccccccccccccccccccccccccccccccc");

        ReputationAwardKey.Verification(ReputationActions.CountryVerified, GuideId).Value
            .Should().Be("CountryVerified:verification:cccccccccccccccccccccccccccccccc");
    }

    [Fact]
    public void A_helpful_vote_is_keyed_by_voter_as_well_as_guide()
    {
        ReputationAwardKey.HelpfulVote(GuideId, VoterA)
            .Should().NotBe(ReputationAwardKey.HelpfulVote(GuideId, VoterB));

        ReputationAwardKey.HelpfulVote(GuideId, VoterA)
            .Should().Be(ReputationAwardKey.HelpfulVote(GuideId, VoterA));
    }

    /// <summary>
    /// Truncating would collide two long identities into one award; hashing keeps them apart and
    /// still fits the column.
    /// </summary>
    [Fact]
    public void An_overlong_key_is_hashed_rather_than_truncated()
    {
        var prefix = new string('x', 300);

        var first = ReputationAwardKey.Confirmation("post", prefix + "1", VoterA);
        var second = ReputationAwardKey.Confirmation("post", prefix + "2", VoterA);

        first.Value.Length.Should().BeLessThanOrEqualTo(ReputationAwardKey.MaxLength);
        second.Value.Length.Should().BeLessThanOrEqualTo(ReputationAwardKey.MaxLength);
        first.Should().NotBe(second);
    }

    [Fact]
    public void A_confirmation_key_ignores_the_casing_of_its_target_type()
    {
        ReputationAwardKey.Confirmation("Post", "42", VoterA)
            .Should().Be(ReputationAwardKey.Confirmation("post", "42", VoterA));
    }

    // ── The service ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_award_already_made_is_skipped_before_any_cap_is_read()
    {
        var harness = new Harness();
        harness.Reputations
            .HasAwardAsync(Author, Arg.Any<ReputationAwardKey>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var outcome = await harness.Service.ScoreAsync(
            Author, ReputationActions.CommentWritten, ReputationAwardKey.Comment(Guid.NewGuid()));

        outcome.WasApplied.Should().BeFalse();
        outcome.SkipReason.Should().Be("Already awarded for this.");
        await harness.Reputations.DidNotReceiveWithAnyArgs()
            .GetRuleUsageAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task The_event_carries_the_key_it_was_awarded_for()
    {
        var harness = new Harness();
        var key = ReputationAwardKey.Comment(Guid.NewGuid());

        await harness.Service.ScoreAsync(Author, ReputationActions.CommentWritten, key);

        harness.Profile.Events.Should().ContainSingle()
            .Which.AwardKey.Should().Be(key.Value);
    }

    // ── The ways it used to pay twice ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_redelivered_guide_published_event_pays_once()
    {
        var harness = new Harness();
        var published = GuidePublished(isFirstPublication: true);

        await DeliverAsync(new GuideReputationConsumer(
            harness.Service, harness.Provisioner, harness.UnitOfWork), published);
        await DeliverAsync(new GuideReputationConsumer(
            harness.Service, harness.Provisioner, harness.UnitOfWork), published);

        harness.Profile.GuidesPublished.Should().Be(1);
        harness.Profile.ContributionScore.Should().Be(50);
    }

    /// <summary>
    /// Guides raises the event for every version. A guide first published before awards were
    /// keyed has no key to collide with, so only <c>IsFirstPublication</c> stops its new version
    /// paying again.
    /// </summary>
    [Fact]
    public async Task Publishing_a_new_version_of_a_guide_pays_nothing()
    {
        var harness = new Harness();

        await DeliverAsync(new GuideReputationConsumer(
            harness.Service, harness.Provisioner, harness.UnitOfWork),
            GuidePublished(isFirstPublication: false));

        harness.Profile.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task A_helpful_vote_changed_away_and_back_pays_once()
    {
        var harness = new Harness();

        foreach (var (kind, previous) in new[]
                 {
                     ("Helpful", (string?)null),
                     ("NotHelpful", "Helpful"),
                     ("Helpful", "NotHelpful")
                 })
        {
            await DeliverAsync(
                new GuideVoteReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
                Vote(VoterA, kind, previous));
        }

        harness.Profile.HelpfulVotesReceived.Should().Be(1);
        harness.Profile.TrustScore.Should().Be(2);
    }

    [Fact]
    public async Task Two_members_voting_the_same_guide_helpful_both_pay()
    {
        var harness = new Harness();

        await DeliverAsync(
            new GuideVoteReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            Vote(VoterA, "Helpful", null));
        await DeliverAsync(
            new GuideVoteReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            Vote(VoterB, "Helpful", null));

        harness.Profile.HelpfulVotesReceived.Should().Be(2);
    }

    /// <summary>
    /// The consumer's own comment credited a single-occurrence cap the rule never had.
    /// </summary>
    [Fact]
    public async Task A_guide_verified_twice_pays_its_approval_once()
    {
        var harness = new Harness();

        foreach (var state in new[] { "Verified", "Unverified", "Verified" })
        {
            await DeliverAsync(
                new GuideVerifiedReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
                new GuideVerificationChangedIntegrationEvent
                {
                    GuideId = GuideId,
                    State = state,
                    ChangedAt = Now,
                    AuthorUserId = AuthorGuid
                });
        }

        harness.Profile.GuidesApproved.Should().Be(1);
        harness.Profile.ContributionScore.Should().Be(25);
    }

    [Fact]
    public async Task A_redelivered_comment_event_pays_once_and_a_second_comment_still_pays()
    {
        var harness = new Harness();
        var first = Comment(Guid.NewGuid());

        await DeliverAsync(
            new CommunityCommentReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            first);
        await DeliverAsync(
            new CommunityCommentReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            first);
        await DeliverAsync(
            new CommunityCommentReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            Comment(Guid.NewGuid()));

        harness.Profile.CommentsWritten.Should().Be(2);
    }

    /// <summary>
    /// A guide update is keyed on the delivered event, so redelivery pays once while a genuinely
    /// new update is left to the rule's cooldown — zero here, to show the key did the work.
    /// </summary>
    [Fact]
    public async Task A_redelivered_guide_update_pays_once()
    {
        var harness = new Harness();
        var updated = new GuideUpdatedIntegrationEvent
        {
            GuideId = GuideId,
            ChangedFields = ["body"],
            ChangedAt = Now,
            AuthorUserId = AuthorGuid,
            IsReadableByOthers = true
        };

        await DeliverAsync(
            new GuideUpdatedReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            updated);
        await DeliverAsync(
            new GuideUpdatedReputationConsumer(harness.Service, harness.Provisioner, harness.UnitOfWork),
            updated);

        harness.Profile.GuidesUpdated.Should().Be(1);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static GuidePublishedIntegrationEvent GuidePublished(bool isFirstPublication) => new()
    {
        GuideId = GuideId,
        Slug = "opening-a-bank-account",
        Title = "Opening a bank account",
        CategoryKey = "finance",
        Tags = [],
        CountryCode = "MU",
        Language = "en",
        AuthorId = AuthorGuid,
        Version = isFirstPublication ? 1 : 2,
        IsFirstPublication = isFirstPublication,
        PublishedAt = Now
    };

    private static GuideVoteCastIntegrationEvent Vote(Guid voter, string kind, string? previous) => new()
    {
        GuideId = GuideId,
        AuthorUserId = AuthorGuid,
        VoterUserId = voter,
        Kind = kind,
        PreviousKind = previous,
        CastAt = Now
    };

    private static CommunityCommentAddedIntegrationEvent Comment(Guid commentId) => new()
    {
        CommentId = commentId,
        ContentType = "post",
        ContentId = Guid.NewGuid(),
        AuthorId = AuthorGuid,
        Depth = 0
    };

    private sealed class Harness
    {
        public Harness()
        {
            Profile = ReputationProfile.Open(Author, Now);
            var trust = TrustProfile.Open(Author, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            Reputations = Substitute.For<IReputationProfileRepository>();
            Reputations.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(Profile);
            Reputations
                .HasAwardAsync(Author, Arg.Any<ReputationAwardKey>(), Arg.Any<CancellationToken>())
                .Returns(call => Profile.Events.Any(
                    recorded => recorded.AwardKey == call.Arg<ReputationAwardKey>().Value));

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(trust);

            UnitOfWork = Substitute.For<IUnitOfWork>();

            Service = new ReputationService(Reputations, profiles, new ReputationRuleCatalog([]), clock);
            Provisioner = new TrustProfileProvisioner(
                profiles, Reputations, Substitute.For<ITrustEventRepository>(), clock);
        }

        public ReputationProfile Profile { get; }

        public IReputationProfileRepository Reputations { get; }

        public ReputationService Service { get; }

        public TrustProfileProvisioner Provisioner { get; }

        public IUnitOfWork UnitOfWork { get; }
    }

    private static Task DeliverAsync<TEvent>(
        INotificationHandler<IntegrationEventNotification<TEvent>> consumer, TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        consumer.Handle(
            new IntegrationEventNotification<TEvent>(integrationEvent), CancellationToken.None);
}
