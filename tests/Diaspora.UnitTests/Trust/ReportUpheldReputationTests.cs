using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Content.V1.Events;
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
/// A report a moderator upheld pays the member who raised it (REP-13, step 3e).
/// <para>
/// <c>ReportResolved</c> was a rule nothing paid: Trust's own moderation queue is never fed, and the
/// five modules that decide reports said nothing about how. They now publish one shared contract, and
/// Trust pays the reporter 5 trust XP for each upheld report, once, with a one-hour cooldown.
/// </para>
/// <para>
/// <b>The award is sourced from the report, not the content.</b> Removing content takes back every
/// award sourced from it (step 3b, B1), and an upheld report usually removes the content; an award
/// sourced from the content would be taken back the moment it was paid.
/// </para>
/// </summary>
public sealed class ReportUpheldReputationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid ReporterGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly UserId Reporter = UserId.From(ReporterGuid);

    private static readonly Guid ContentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void An_upheld_report_is_priced_at_five_trust_points_once_an_hour_and_pays_no_reward_points()
    {
        var rule = DefaultReputationRules.All.Should()
            .ContainSingle(item => item.ActionType == ReputationActions.ReportResolved).Subject;

        rule.Dimension.Should().Be(ReputationDimension.trust);
        rule.Points.Should().Be(5);
        rule.CooldownHours.Should().Be(1);
        rule.IsAwarded.Should().BeTrue();
        rule.CreditPoints.Should().BeNull("a report earns XP alone (D-2)");
        DefaultReputationRules.Version.Should().Be(6);
    }

    [Theory]
    [InlineData("community")]
    [InlineData("community-life")]
    [InlineData("housing")]
    [InlineData("jobs")]
    [InlineData("marketplace")]
    public async Task An_upheld_report_pays_its_reporter_once_however_often_it_is_delivered(string module)
    {
        var harness = new Harness();
        var upheld = Upheld(module);

        await harness.DeliverAsync(upheld);
        await harness.DeliverAsync(upheld);

        var award = harness.Profile.Events.Should().ContainSingle().Subject;
        award.ActionType.Should().Be(ReputationActions.ReportResolved);
        award.Points.Should().Be(5);
        award.SourceType.Should().Be("report");
        award.SourceId.Should().Be(upheld.ReportId.ToString());
        award.AwardKey.Should().Be(ReputationAwardKey.Report(module, upheld.ReportId).Value);
    }

    [Fact]
    public void Two_modules_reports_with_one_id_are_two_awards()
    {
        var reportId = Guid.NewGuid();

        ReputationAwardKey.Report("jobs", reportId).Should().NotBe(ReputationAwardKey.Report("housing", reportId));
    }

    [Fact]
    public async Task Reporting_your_own_content_pays_nothing()
    {
        var harness = new Harness();

        await harness.DeliverAsync(Upheld("community") with { ContentOwnerUserId = ReporterGuid });

        harness.Profile.Events.Should().BeEmpty();
        await harness.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    /// <summary>The removal an upheld report causes takes back the author's XP, never the reporter's.</summary>
    [Fact]
    public async Task Removing_the_reported_content_does_not_take_the_reporters_XP_back()
    {
        var harness = new Harness();
        await harness.DeliverAsync(Upheld("community"));

        await harness.Service.ReverseBySourceAsync(
            null, ContentId.ToString(), "Post removed.", ReversalScope.Awards);

        harness.Profile.Events.Should().ContainSingle()
            .Which.ActionType.Should().Be(ReputationActions.ReportResolved);
    }

    private static ContentReportUpheldIntegrationEvent Upheld(string module) => new()
    {
        ReportId = Guid.NewGuid(),
        Module = module,
        ContentType = "post",
        ContentId = ContentId,
        ReporterUserId = ReporterGuid,
        ContentOwnerUserId = Guid.NewGuid(),
        UpheldAt = Now
    };

    private sealed class Harness
    {
        public Harness()
        {
            Profile = ReputationProfile.Open(Reporter, Now);
            var trust = TrustProfile.Open(Reporter, Now);

            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);

            var reputations = Substitute.For<IReputationProfileRepository>();
            reputations.GetByUserAsync(Reporter, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations.GetByIdAsync(Profile.Id, Arg.Any<CancellationToken>()).Returns(Profile);
            reputations
                .HasAwardAsync(Reporter, Arg.Any<ReputationAwardKey>(), Arg.Any<CancellationToken>())
                .Returns(call => Profile.Events.Any(
                    recorded => recorded.AwardKey == call.Arg<ReputationAwardKey>().Value));
            reputations
                .ListUnreversedBySourceAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => (IReadOnlyList<ReputationEvent>)[.. Profile.Events.Where(item =>
                    item.SourceId == call.ArgAt<string>(1)
                    && !item.IsReversal
                    && !Profile.Events.Any(reversal => reversal.ReversesEventId == item.Id))]);

            var profiles = Substitute.For<ITrustProfileRepository>();
            profiles.GetByUserAsync(Reporter, Arg.Any<CancellationToken>()).Returns(trust);

            UnitOfWork = Substitute.For<IUnitOfWork>();
            Service = new ReputationService(
                reputations, profiles, new ReputationRuleCatalog([]), Substitute.For<IIntegrationEventPublisher>(), clock);
            var provisioner = new TrustProfileProvisioner(
                profiles, reputations, Substitute.For<ITrustEventRepository>(), clock);
            Consumer = new ReportUpheldReputationConsumer(new ActivityReputationScorer(Service, provisioner, UnitOfWork));
        }

        public ReputationProfile Profile { get; }

        public ReputationService Service { get; }

        public ReportUpheldReputationConsumer Consumer { get; }

        public IUnitOfWork UnitOfWork { get; }

        public Task DeliverAsync(ContentReportUpheldIntegrationEvent upheld) =>
            Consumer.Handle(
                new IntegrationEventNotification<ContentReportUpheldIntegrationEvent>(upheld),
                CancellationToken.None);
    }
}
