using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Consumers.Rewards;
using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Trust.Application.EventHandlers;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.Modules.Trust.Options;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// A member whose risk becomes High has their points held for review (RWD-14, CS-4b, step 3f).
/// <para>
/// Nothing computed risk, so every member sat at the baseline and the rule that pauses publishing
/// for a High member never fired. Trust now counts two kinds of evidence: a verification a reviewer
/// rejected as fraud-like (Q7), and upheld reports against three different pieces of the member's
/// content in a week (Q8). Either alone is Medium; both are High. Trust announces the level, never the
/// score, and the Financial Platform holds conversion, decided by nobody, until staff or an appeal
/// lifts it. A falling level lifts nothing: signals flag, staff decide.
/// </para>
/// </summary>
public sealed class RiskHoldTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static readonly VerificationType[] NoGrants = [];

    // ── What counts ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(VerificationRejectionReason.SUSPECTED_FRAUD)]
    [InlineData(VerificationRejectionReason.FACE_MATCH_FAILED)]
    [InlineData(VerificationRejectionReason.DUPLICATE_SUBMISSION)]
    public void A_rejection_a_reviewer_judged_fraud_like_counts(VerificationRejectionReason reason)
    {
        RiskPolicy.CountsAgainstMember(reason).Should().BeTrue();
    }

    [Theory]
    [InlineData(VerificationRejectionReason.INVALID_DOCUMENT)]
    [InlineData(VerificationRejectionReason.EXPIRED_DOCUMENT)]
    [InlineData(VerificationRejectionReason.MISMATCHED_NATIONALITY)]
    [InlineData(VerificationRejectionReason.ADDITIONAL_DOCS_NEEDED)]
    [InlineData(VerificationRejectionReason.ILLEGIBLE_DOCUMENT)]
    [InlineData(VerificationRejectionReason.RESIDENCE_NOT_ESTABLISHED)]
    [InlineData(VerificationRejectionReason.NOT_DIASPORA_ELIGIBLE)]
    public void An_honest_rejection_counts_for_nothing(VerificationRejectionReason reason)
    {
        RiskPolicy.CountsAgainstMember(reason).Should().BeFalse("a blurred photo is not fraud");
    }

    [Fact]
    public void Evidence_is_keyed_by_where_it_came_from()
    {
        var verificationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var contentId = Guid.NewGuid();

        RiskSignal.VerificationRejected(Member, verificationId, VerificationType.Identity, Now)
            .SourceKey.Should().Be($"verification:{verificationId:N}");

        var report = RiskSignal.ReportUpheld(Member, "Jobs", "job", contentId, reportId, Now);
        report.SourceKey.Should().Be($"report:Jobs:{reportId:N}");
        report.ContentKey.Should().Be($"Jobs/job/{contentId:N}");
    }

    // ── The policy ───────────────────────────────────────────────────────

    [Fact]
    public void A_fraud_like_rejection_within_thirty_days_is_a_failed_verification()
    {
        RiskPolicy.Assess([Rejected(Now.AddDays(-29))], NoGrants, Now)
            .Factors.Should().Equal(RiskFactor.Failed_Verification);
    }

    [Fact]
    public void A_rejection_older_than_thirty_days_has_aged_out()
    {
        RiskPolicy.Assess([Rejected(Now.AddDays(-31))], NoGrants, Now)
            .Should().Be(RiskAssessment.Baseline(Now));
    }

    [Fact]
    public void A_later_grant_of_the_same_verification_answers_the_rejection()
    {
        RiskPolicy.Assess([Rejected(Now.AddDays(-2), VerificationType.Identity)], [VerificationType.Identity], Now)
            .Factors.Should().BeEmpty();

        RiskPolicy.Assess([Rejected(Now.AddDays(-2), VerificationType.Identity)], [VerificationType.Phone], Now)
            .Factors.Should().Equal(RiskFactor.Failed_Verification);
    }

    [Fact]
    public void Three_pieces_of_content_upheld_within_a_week_are_a_spike()
    {
        RiskPolicy.Assess([Upheld(Now.AddDays(-6)), Upheld(Now.AddDays(-3)), Upheld(Now)], NoGrants, Now)
            .Factors.Should().Equal(RiskFactor.Reports_Spike);
    }

    [Fact]
    public void Reports_upheld_on_one_piece_of_content_are_one()
    {
        var post = Guid.NewGuid();

        RiskPolicy.Assess(
                [Upheld(Now, contentId: post), Upheld(Now, contentId: post), Upheld(Now, contentId: post)],
                NoGrants,
                Now)
            .Factors.Should().BeEmpty("three people reporting one post is one bad post");
    }

    [Fact]
    public void Two_pieces_of_content_are_not_a_spike_nor_is_a_third_from_last_week()
    {
        RiskPolicy.Assess([Upheld(Now), Upheld(Now.AddDays(-1))], NoGrants, Now)
            .Factors.Should().BeEmpty();

        RiskPolicy.Assess([Upheld(Now), Upheld(Now.AddDays(-1)), Upheld(Now.AddDays(-8))], NoGrants, Now)
            .Factors.Should().BeEmpty();
    }

    [Fact]
    public void Either_kind_of_evidence_alone_is_medium_and_both_together_are_high()
    {
        RiskPolicy.Assess([Upheld(Now), Upheld(Now), Upheld(Now)], NoGrants, Now).Level.Should().Be(RiskLevel.Medium);
        RiskPolicy.Assess([Rejected(Now)], NoGrants, Now).Level.Should().Be(RiskLevel.Medium);

        var both = RiskPolicy.Assess([Rejected(Now), Upheld(Now), Upheld(Now), Upheld(Now)], NoGrants, Now);

        both.Level.Should().Be(RiskLevel.High);
        both.Factors.Should().BeEquivalentTo([RiskFactor.Failed_Verification, RiskFactor.Reports_Spike]);
    }

    // ── The level, announced ─────────────────────────────────────────────

    [Fact]
    public void A_change_of_level_is_announced_and_a_reassessment_at_the_same_level_is_not()
    {
        var profile = TrustProfile.Open(Member, Now);
        var high = RiskAssessment.From([RiskFactor.Failed_Verification, RiskFactor.Reports_Spike], Now);

        profile.UpdateRisk(high);

        profile.DomainEvents.OfType<MemberRiskLevelChangedDomainEvent>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                UserId = Member,
                From = RiskLevel.Low,
                To = RiskLevel.High,
                At = Now
            });

        profile.ClearDomainEvents();
        profile.UpdateRisk(RiskAssessment.From([RiskFactor.Failed_Verification, RiskFactor.Reports_Spike], Now.AddHours(1)));

        profile.DomainEvents.OfType<MemberRiskLevelChangedDomainEvent>().Should().BeEmpty();
        profile.Risk.AssessedAt.Should().Be(Now.AddHours(1), "the assessment is still renewed");
    }

    [Fact]
    public async Task The_published_change_carries_the_level_and_never_the_score()
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var handler = new MemberRiskLevelChangedEventHandler(publisher);

        await handler.Handle(
            new DomainEventNotification<MemberRiskLevelChangedDomainEvent>(new MemberRiskLevelChangedDomainEvent(
                TrustProfileId.New(), Member, RiskLevel.Medium, RiskLevel.High, Now)),
            CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            Arg.Is<IIntegrationEvent>(published =>
                published is MemberRiskLevelChangedIntegrationEvent
                && ((MemberRiskLevelChangedIntegrationEvent)published).UserId == Member.Value
                && ((MemberRiskLevelChangedIntegrationEvent)published).CurrentLevel == "High"
                && ((MemberRiskLevelChangedIntegrationEvent)published).PreviousLevel == "Medium"
                && ((MemberRiskLevelChangedIntegrationEvent)published).ChangedAt == Now),
            Arg.Any<CancellationToken>());

        typeof(MemberRiskLevelChangedIntegrationEvent).GetProperties().Select(property => property.Name)
            .Should().NotContain(["Score", "RiskScore", "Factors"], "the score and its factors are staff-only");
    }

    // ── Recording evidence ───────────────────────────────────────────────

    [Fact]
    public async Task With_the_switch_off_nothing_is_recorded_or_assessed()
    {
        var harness = new AssessorHarness(enabled: false);

        await harness.Assessor.RecordAsync(Rejected(Now), CancellationToken.None);

        await harness.Signals.DidNotReceive().AddAsync(Arg.Any<RiskSignal>(), Arg.Any<CancellationToken>());
        await harness.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        harness.Profile.Risk.Should().Be(RiskAssessment.Baseline(Now));
    }

    [Fact]
    public async Task New_evidence_is_stored_and_the_member_reassessed_in_one_save()
    {
        var harness = new AssessorHarness(enabled: true, Upheld(Now.AddDays(-2)), Upheld(Now.AddDays(-1)));
        var third = Upheld(Now);

        await harness.Assessor.RecordAsync(third, CancellationToken.None);

        await harness.Signals.Received(1).AddAsync(third, Arg.Any<CancellationToken>());
        await harness.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        harness.Profile.Risk.Factors.Should().Equal(RiskFactor.Reports_Spike);
    }

    [Fact]
    public async Task Evidence_delivered_twice_is_stored_once()
    {
        var known = Upheld(Now);
        var harness = new AssessorHarness(enabled: true, known);
        harness.Signals.ExistsAsync(known.SourceKey, Arg.Any<CancellationToken>()).Returns(true);

        await harness.Assessor.RecordAsync(known, CancellationToken.None);

        await harness.Signals.DidNotReceive().AddAsync(Arg.Any<RiskSignal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evidence_older_than_any_window_is_not_stored()
    {
        var harness = new AssessorHarness(enabled: true);

        await harness.Assessor.RecordAsync(Upheld(Now.AddDays(-40)), CancellationToken.None);

        await harness.Signals.DidNotReceive().AddAsync(Arg.Any<RiskSignal>(), Arg.Any<CancellationToken>());
    }

    // ── The hold ─────────────────────────────────────────────────────────

    [Fact]
    public async Task High_holds_the_points_decided_by_nobody()
    {
        var account = RewardAccount.Open(RewardAccountId.From(Guid.NewGuid()), Member.Value, Now);
        var harness = new HoldHarness(account);

        await harness.DeliverAsync("High", "Medium");

        account.IsConversionBlocked.Should().BeTrue();
        account.HoldDecisions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IsHeld = true,
            Reason = MemberRiskHighRewardHoldConsumer.Reason,
            DecidedByUserId = (Guid?)null
        });
        await harness.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_member_with_no_points_yet_is_held_all_the_same()
    {
        var harness = new HoldHarness(account: null);

        await harness.DeliverAsync("High", "Low");

        harness.Accounts.Received(1).Add(Arg.Is<RewardAccount>(opened =>
            opened.MemberUserId == Member.Value && opened.IsConversionBlocked));
    }

    [Fact]
    public async Task A_hold_already_in_force_keeps_its_note()
    {
        var account = RewardAccount.Open(RewardAccountId.From(Guid.NewGuid()), Member.Value, Now);
        account.BlockConversion(Guid.NewGuid(), "Staff: checking a payout.", Guid.NewGuid(), Now);
        var harness = new HoldHarness(account);

        await harness.DeliverAsync("High", "Medium");

        account.HoldDecisions.Should().ContainSingle().Which.Reason.Should().Be("Staff: checking a payout.");
    }

    [Theory]
    [InlineData("Medium", "Low")]
    [InlineData("Low", "High")]
    [InlineData("Medium", "High")]
    public async Task Anything_short_of_high_changes_no_hold(string level, string previous)
    {
        var account = RewardAccount.Open(RewardAccountId.From(Guid.NewGuid()), Member.Value, Now);
        account.BlockConversion(Guid.NewGuid(), MemberRiskHighRewardHoldConsumer.Reason, null, Now);
        var harness = new HoldHarness(account);

        await harness.DeliverAsync(level, previous);

        account.IsConversionBlocked.Should().BeTrue("a falling level lifts nothing: staff decide");
        account.HoldDecisions.Should().ContainSingle();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static RiskSignal Rejected(
        DateTimeOffset at, VerificationType type = VerificationType.Identity) =>
        RiskSignal.VerificationRejected(Member, Guid.NewGuid(), type, at);

    private static RiskSignal Upheld(DateTimeOffset at, Guid? contentId = null) =>
        RiskSignal.ReportUpheld(Member, "Community", "post", contentId ?? Guid.NewGuid(), Guid.NewGuid(), at);

    private sealed class AssessorHarness
    {
        public AssessorHarness(bool enabled, params RiskSignal[] recorded)
        {
            Profile = TrustProfile.Open(Member, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Member, Arg.Any<CancellationToken>()).Returns(Profile);

            Signals = Substitute.For<IRiskSignalRepository>();
            Signals
                .ListSinceAsync(Member, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<RiskSignal>)recorded);

            UnitOfWork = Substitute.For<IUnitOfWork>();

            var provisioner = new TrustProfileProvisioner(
                profiles, Substitute.For<IReputationProfileRepository>(), Substitute.For<ITrustEventRepository>(), clock);

            Assessor = new MemberRiskAssessor(
                Signals,
                profiles,
                provisioner,
                MicrosoftOptions.Create(new TrustOptions { EnableRiskAssessment = enabled }),
                clock,
                UnitOfWork);
        }

        public TrustProfile Profile { get; }

        public IRiskSignalRepository Signals { get; }

        public IUnitOfWork UnitOfWork { get; }

        public MemberRiskAssessor Assessor { get; }
    }

    private sealed class HoldHarness
    {
        public HoldHarness(RewardAccount? account)
        {
            Accounts = Substitute.For<IRewardAccountRepository>();
            Accounts.FindByMemberAsync(Member.Value, Arg.Any<CancellationToken>()).Returns(account);

            var guids = Substitute.For<IGuidGenerator>();
            guids.NewSequentialGuid().Returns(_ => Guid.NewGuid());

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            UnitOfWork = Substitute.For<IUnitOfWork>();

            Consumer = new MemberRiskHighRewardHoldConsumer(
                new SystemRewardHolds(Accounts, guids, clock, UnitOfWork));
        }

        public IRewardAccountRepository Accounts { get; }

        public IUnitOfWork UnitOfWork { get; }

        public MemberRiskHighRewardHoldConsumer Consumer { get; }

        public Task DeliverAsync(string level, string previous) =>
            Consumer.Handle(
                new IntegrationEventNotification<MemberRiskLevelChangedIntegrationEvent>(
                    new MemberRiskLevelChangedIntegrationEvent
                    {
                        UserId = Member.Value,
                        CurrentLevel = level,
                        PreviousLevel = previous,
                        ChangedAt = Now
                    }),
                CancellationToken.None);
    }
}
