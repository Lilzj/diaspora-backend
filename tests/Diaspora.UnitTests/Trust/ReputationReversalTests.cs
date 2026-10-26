using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Consumers;
using Diaspora.Modules.Trust.Application.Mapping;
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
/// What content earned comes back with the content (D-9, REP-12).
/// <para>
/// Nothing took an award back. A guide published and deleted kept its 50 points, a removed comment
/// kept its 2, a revoked verification kept its award, and a fine overturned on appeal stayed taken.
/// A reversal is a compensating event that names the original, so the history stays append-only.
/// </para>
/// <para>
/// The harness keeps one profile and answers the repository's lookups from that profile's own
/// events, which is what the database returns once a delivery has saved.
/// </para>
/// </summary>
public sealed class ReputationReversalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorGuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly UserId Author = UserId.From(AuthorGuid);

    // ── The profile ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reversing_an_award_takes_back_its_points_and_its_count()
    {
        var profile = ReputationProfile.Open(Author, Now);
        var original = profile.Apply(
            ReputationAward.Of(ReputationActions.GuidePublished, ReputationDimension.contribution, 50),
            "guide", "guide-1", "GB", Now);

        var reversal = profile.Reverse(
            original, "Guide deleted.", Now.AddHours(1), ReputationAwardKey.Reversal(original.Id));

        profile.ContributionScore.Should().Be(0);
        profile.GuidesPublished.Should().Be(0);

        reversal.ReversesEventId.Should().Be(original.Id);
        reversal.IsReversal.Should().BeTrue();
        reversal.Points.Should().Be(-50);
        reversal.ActionType.Should().Be(ReputationActions.GuidePublished,
            "a reversal shares its original's action, so the breakdown and the caps net them");
        reversal.SourceType.Should().Be("guide");
        reversal.SourceId.Should().Be("guide-1");
        reversal.CountryCode.Should().Be("GB");
        reversal.AwardKey.Should().Be(ReputationAwardKey.Reversal(original.Id).Value);

        profile.Events.Should().HaveCount(2, "the original stays: the history is append-only");
    }

    /// <summary>
    /// A 75-point fine against a score of 10 took 10. Refunding 75 would hand the member 65 points
    /// they never had.
    /// </summary>
    [Fact]
    public void Refunding_a_fine_gives_back_only_what_it_took()
    {
        var profile = ReputationProfile.Open(Author, Now);
        profile.Apply(ReputationAward.Of("Seed", ReputationDimension.trust, 10), null, null, null, Now);

        var fine = profile.Apply(
            ReputationAward.Of(ReputationActions.ViolationRecorded, ReputationDimension.trust, -75),
            "post", "post-1", null, Now);

        fine.AppliedPoints.Should().Be(-10);
        profile.TrustScore.Should().Be(0);

        var refund = profile.Reverse(
            fine, "Appeal upheld.", Now.AddDays(1), ReputationAwardKey.Reversal(fine.Id));

        refund.AppliedPoints.Should().Be(10);
        refund.Points.Should().Be(75, "it records the original's opposite, so the two net to nothing");
        profile.TrustScore.Should().Be(10);
    }

    /// <summary>
    /// The breakdown sums recorded points and puts what the floor absorbed in its adjustment. A
    /// refunded fine has to leave no row behind and no adjustment either: the member is back where
    /// they were.
    /// </summary>
    [Fact]
    public void A_refunded_fine_leaves_nothing_in_the_breakdown()
    {
        var profile = ReputationProfile.Open(Author, Now);
        profile.Apply(ReputationAward.Of(ReputationActions.ReportResolved, ReputationDimension.trust, 10), null, null, null, Now);
        var fine = profile.Apply(
            ReputationAward.Of(ReputationActions.ViolationRecorded, ReputationDimension.trust, -75),
            "post", "post-1", null, Now);
        var refund = profile.Reverse(fine, "Appeal upheld.", Now, ReputationAwardKey.Reversal(fine.Id));

        var trust = profile
            .ToBreakdown(
                [
                    new(ReputationDimension.trust, ReputationActions.ReportResolved, 1, 10),
                    new(ReputationDimension.trust, ReputationActions.ViolationRecorded, 0, fine.Points + refund.Points)
                ],
                new ReputationRuleCatalog([]))
            .Single(dimension => dimension.Dimension == "trust");

        trust.Score.Should().Be(10);
        trust.Lines.Should().ContainSingle().Which.ActionType.Should().Be(ReputationActions.ReportResolved);
        trust.Adjustment.Should().Be(0);
    }

    [Fact]
    public void A_refunded_violation_leaves_the_clean_streak_where_it_was()
    {
        var profile = ReputationProfile.Open(Author, Now);
        profile.Apply(ReputationAward.Of("Seed", ReputationDimension.trust, 200), null, null, null, Now);

        var violation = profile.Apply(
            ReputationAward.Of(ReputationActions.ViolationRecorded, ReputationDimension.trust, -75),
            null, null, null, Now.AddDays(30));

        profile.Reverse(
            violation, "Appeal upheld.", Now.AddDays(35), ReputationAwardKey.Reversal(violation.Id));

        profile.ViolationCount.Should().Be(0);
        profile.TrustScore.Should().Be(200);
        profile.NoViolationStreakDays(Now.AddDays(40)).Should().Be(10,
            "the streak is a fact about time; a refund does not rewrite when it restarted");
    }

    [Fact]
    public void Taking_back_an_award_can_take_back_the_level_it_bought()
    {
        var profile = ReputationProfile.Open(Author, Now);
        var award = profile.Apply(
            ReputationAward.Of("Bulk", ReputationDimension.contribution, ReputationThresholds.VerifiedResident),
            null, null, null, Now);

        profile.Level.Should().Be(ReputationLevel.VerifiedResident);

        profile.Reverse(award, "Removed.", Now.AddHours(1), ReputationAwardKey.Reversal(award.Id));

        profile.Level.Should().Be(ReputationLevel.Member);
        profile.LevelChangedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void A_reversal_is_never_itself_reversed_nor_one_profile_reversed_against_another()
    {
        var profile = ReputationProfile.Open(Author, Now);
        var award = profile.Apply(
            ReputationAward.Of(ReputationActions.CommentWritten, ReputationDimension.contribution, 2),
            "comment", "c-1", null, Now);
        var reversal = profile.Reverse(award, "Removed.", Now, ReputationAwardKey.Reversal(award.Id));

        var undoUndo = () => profile.Reverse(
            reversal, "Again.", Now, ReputationAwardKey.Reversal(reversal.Id));
        undoUndo.Should().Throw<InvalidOperationException>();

        var stranger = ReputationProfile.Open(UserId.From(Guid.NewGuid()), Now);
        var crossed = () => stranger.Reverse(
            award, "Removed.", Now, ReputationAwardKey.Reversal(award.Id));
        crossed.Should().Throw<InvalidOperationException>();
    }

    // ── The consumers ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_guide_takes_back_everything_it_earned()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.GuidePublished,
            ReputationAwardKey.GuidePublished(guideId), "guide", guideId);
        await harness.ScoreAsync(ReputationActions.HelpfulVoteReceived,
            ReputationAwardKey.HelpfulVote(guideId, Guid.NewGuid()), "guide", guideId);

        await DeliverAsync(harness.Consumer<GuideDeletedReversalConsumer>(), GuideDeleted(guideId));

        harness.Profile.ContributionScore.Should().Be(0);
        harness.Profile.TrustScore.Should().Be(0);
        harness.Profile.GuidesPublished.Should().Be(0);
        harness.Profile.HelpfulVotesReceived.Should().Be(0);

        var awards = harness.Profile.Events.Where(item => !item.IsReversal).ToList();
        harness.Profile.Events.Where(item => item.IsReversal)
            .Select(item => item.ReversesEventId)
            .Should().BeEquivalentTo(awards.Select(item => item.Id));
    }

    /// <summary>The outbox delivers at least once. A deletion delivered twice takes the award back once.</summary>
    [Fact]
    public async Task A_second_delivery_reverses_nothing()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.GuidePublished,
            ReputationAwardKey.GuidePublished(guideId), "guide", guideId);

        var deleted = GuideDeleted(guideId);
        await DeliverAsync(harness.Consumer<GuideDeletedReversalConsumer>(), deleted);
        await DeliverAsync(harness.Consumer<GuideDeletedReversalConsumer>(), deleted);

        harness.Profile.Events.Count(item => item.IsReversal).Should().Be(1);
        harness.Profile.ContributionScore.Should().Be(0);
    }

    /// <summary><c>CommunityCommentRemoved</c> names no author. The award is found by the comment.</summary>
    [Fact]
    public async Task A_removed_comment_is_found_without_its_author()
    {
        var harness = new Harness();
        var commentId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.CommentWritten,
            ReputationAwardKey.Comment(commentId), "comment", commentId);

        await DeliverAsync(harness.Consumer<CommunityCommentRemovedReversalConsumer>(),
            new CommunityCommentRemovedIntegrationEvent
            {
                CommentId = commentId,
                ContentType = "post",
                ContentId = Guid.NewGuid(),
                ByModerator = true
            });

        harness.Profile.CommentsWritten.Should().Be(0);
        harness.Profile.Events.Should().ContainSingle(item => item.IsReversal);
    }

    /// <summary>Q4: the knowledge is dated, not wrong.</summary>
    [Fact]
    public async Task An_author_archiving_their_own_guide_keeps_what_it_earned()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.GuidePublished,
            ReputationAwardKey.GuidePublished(guideId), "guide", guideId);

        await DeliverAsync(harness.Consumer<GuideArchivedReversalConsumer>(),
            GuideArchived(guideId, archivedBy: AuthorGuid));

        harness.Profile.Events.Should().NotContain(item => item.IsReversal);
        harness.Profile.ContributionScore.Should().Be(50);
    }

    /// <summary>An archive that names nobody is the moderation projection's: a removal.</summary>
    [Fact]
    public async Task An_archive_by_anyone_else_takes_it_back()
    {
        var harness = new Harness();
        var guideId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.GuidePublished,
            ReputationAwardKey.GuidePublished(guideId), "guide", guideId);

        await DeliverAsync(harness.Consumer<GuideArchivedReversalConsumer>(),
            GuideArchived(guideId, archivedBy: null));

        harness.Profile.ContributionScore.Should().Be(0);
    }

    [Theory]
    [InlineData("expired", 1)]
    [InlineData("deleted", 0)]
    [InlineData("removed", 0)]
    public async Task A_post_keeps_what_it_earned_only_when_it_expires(string reason, int postsLeft)
    {
        var harness = new Harness();
        var postId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.UpdatePublished,
            ReputationAwardKey.CommunityPost(postId), "post", postId);

        await DeliverAsync(harness.Consumer<CommunityPostRemovedReversalConsumer>(),
            new CommunityPostRemovedIntegrationEvent { PostId = postId, AuthorId = AuthorGuid, Reason = reason });

        harness.Profile.UpdatesPublished.Should().Be(postsLeft);
    }

    [Fact]
    public async Task Revoking_a_verification_takes_back_its_award()
    {
        var harness = new Harness();
        var verificationId = Guid.NewGuid();

        await harness.ScoreAsync(ReputationActions.GovernmentIdVerified,
            ReputationAwardKey.Verification(ReputationActions.GovernmentIdVerified, verificationId),
            "verification", verificationId);

        harness.Profile.GovernmentIdVerified.Should().BeTrue();

        await DeliverAsync(harness.Consumer<VerificationRevokedReversalConsumer>(),
            new VerificationRevokedIntegrationEvent
            {
                VerificationId = verificationId,
                UserId = AuthorGuid,
                VerificationType = "Identity",
                Reason = "Document was forged.",
                RevokedAt = Now
            });

        harness.Profile.VerificationScore.Should().Be(0);
        harness.Profile.GovernmentIdVerified.Should().BeFalse();
    }

    /// <summary>
    /// A moderator's removal fines the author and takes back what the content earned (D-9). The
    /// fine is not the content's award: only an upheld appeal gives it back.
    /// </summary>
    [Fact]
    public async Task A_removal_takes_back_the_award_and_keeps_the_fine()
    {
        var harness = new Harness();
        var postId = Guid.NewGuid();
        harness.Seed(ReputationDimension.trust, 100);

        await harness.ScoreAsync(ReputationActions.UpdatePublished,
            ReputationAwardKey.CommunityPost(postId), "post", postId);
        await harness.ScoreAsync(ReputationActions.ContentRemoved,
            ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, Guid.NewGuid()), "Post", postId);

        await DeliverAsync(harness.Consumer<CommunityPostRemovedReversalConsumer>(),
            new CommunityPostRemovedIntegrationEvent { PostId = postId, AuthorId = AuthorGuid, Reason = "removed" });

        harness.Profile.UpdatesPublished.Should().Be(0);
        harness.Profile.TrustScore.Should().Be(75, "the 25-point fine stands");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_upheld_appeal_refunds_the_fine_whichever_id_it_names(bool namesTheContent)
    {
        var harness = new Harness();
        var postId = Guid.NewGuid();
        var moderationItemId = Guid.NewGuid();
        harness.Seed(ReputationDimension.trust, 100);

        await harness.ScoreAsync(ReputationActions.ContentRemoved,
            ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, moderationItemId), "Post", postId);

        harness.Profile.TrustScore.Should().Be(75);

        await DeliverAsync(harness.Consumer<AppealApprovedRefundConsumer>(),
            Appeal("Content", namesTheContent ? postId : moderationItemId));

        harness.Profile.TrustScore.Should().Be(100);
        harness.Profile.Events.Should().ContainSingle(item => item.IsReversal);
    }

    [Fact]
    public async Task A_verification_appeal_refunds_nothing()
    {
        var harness = new Harness();
        var postId = Guid.NewGuid();
        harness.Seed(ReputationDimension.trust, 100);

        await harness.ScoreAsync(ReputationActions.ContentRemoved,
            ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, Guid.NewGuid()), "Post", postId);

        await DeliverAsync(harness.Consumer<AppealApprovedRefundConsumer>(), Appeal("Verification", postId));

        harness.Profile.TrustScore.Should().Be(75);
    }

    /// <summary>
    /// A permit running out is not a finding against the member, and the trust tier already reacts
    /// to the lapse. Nothing in Trust listens for an expiry, so nothing is reversed by one.
    /// </summary>
    [Fact]
    public void An_expiry_reverses_nothing()
    {
        typeof(ReputationService).Assembly.GetTypes()
            .Where(type => typeof(INotificationHandler<IntegrationEventNotification<VerificationExpiredIntegrationEvent>>)
                .IsAssignableFrom(type))
            .Should().BeEmpty("an expired verification keeps the award its approval paid (D-9)");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static GuideDeletedIntegrationEvent GuideDeleted(Guid guideId) => new()
    {
        GuideId = guideId,
        AuthorId = AuthorGuid,
        DeletedAt = Now
    };

    private static GuideArchivedIntegrationEvent GuideArchived(Guid guideId, Guid? archivedBy) => new()
    {
        GuideId = guideId,
        ArchivedBy = archivedBy,
        ArchivedAt = Now,
        AuthorUserId = AuthorGuid
    };

    private static AppealApprovedIntegrationEvent Appeal(string type, Guid targetId) => new()
    {
        AppealId = Guid.NewGuid(),
        UserId = AuthorGuid,
        AppealType = type,
        TargetId = targetId.ToString(),
        DecidedAt = Now
    };

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
                .Returns(call => Unreversed(item =>
                    item.SourceId == call.ArgAt<string>(1)
                    && (call.ArgAt<string?>(0) is not { } type || item.SourceType == type)));
            reputations
                .ListUnreversedByAwardKeysAsync(Author, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(call => Unreversed(item =>
                    item.AwardKey is { } key && call.ArgAt<IReadOnlyCollection<string>>(1).Contains(key)));

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Author, Arg.Any<CancellationToken>()).Returns(trust);

            UnitOfWork = Substitute.For<IUnitOfWork>();
            Service = new ReputationService(reputations, profiles, new ReputationRuleCatalog([]), Substitute.For<IIntegrationEventPublisher>(), clock);
        }

        public ReputationProfile Profile { get; }

        public ReputationService Service { get; }

        public IUnitOfWork UnitOfWork { get; }

        public void Seed(ReputationDimension dimension, int points) =>
            Profile.Apply(ReputationAward.Of("Seed", dimension, points), null, null, null, Now);

        public Task<ScoringOutcome> ScoreAsync(string action, ReputationAwardKey key, string sourceType, Guid sourceId) =>
            Service.ScoreAsync(Author, action, key, sourceType, sourceId.ToString());

        public TConsumer Consumer<TConsumer>() =>
            (TConsumer)Activator.CreateInstance(typeof(TConsumer), Service, UnitOfWork)!;

        private IReadOnlyList<ReputationEvent> Unreversed(Func<ReputationEvent, bool> match) =>
            [.. Profile.Events.Where(item =>
                match(item)
                && !item.IsReversal
                && !Profile.Events.Any(reversal => reversal.ReversesEventId == item.Id))];
    }

    private static Task DeliverAsync<TEvent>(
        INotificationHandler<IntegrationEventNotification<TEvent>> consumer, TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        consumer.Handle(
            new IntegrationEventNotification<TEvent>(integrationEvent), CancellationToken.None);
}
