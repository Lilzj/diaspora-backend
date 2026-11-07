using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Consumers.Rewards;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Infrastructure.Hosting;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A member whose Trust risk becomes High has their points held and publishing paused (RWD-14, CS-4b).
/// <para>
/// End to end through the outbox: the evidence arrives, Trust records it and reassesses the member,
/// announces the level, and the Financial Platform holds the points, decided by nobody. Risk
/// assessment is switched on in this host, as a deployment turns it on; the sweep is driven by hand,
/// as every module's is here.
/// </para>
/// <para>
/// <b>Since 7 Nov 2026 a report counts against the owner only once its removal stands</b> — after the
/// 14-day appeal window, or a refused appeal — and <c>ModerationConsequences</c> records it then.
/// Before, it counted the moment a moderator upheld the report, before the owner could appeal. The
/// tests record that evidence as the consequences do (<see cref="RemovalsStandAsync"/>); the path
/// from a decision to the consequences is <c>ModerationConsequenceTests</c>'.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class RiskHoldTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RiskHoldTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_fraud_rejection_and_three_reported_posts_hold_the_points_and_pause_publishing()
    {
        var (member, auth) = await MemberAsync();

        await AnnounceAsync(Rejection(member, "SUSPECTED_FRAUD"));
        await RemovalsStandAsync(Removal(member), Removal(member), Removal(member));

        var risk = await RiskOfAsync(member);
        risk.Level.Should().Be(RiskLevel.High);
        risk.Factors.Should().BeEquivalentTo([RiskFactor.Failed_Verification, RiskFactor.Reports_Spike]);

        var account = await AccountOfAsync(member);
        account!.IsConversionBlocked.Should().BeTrue();
        account.HoldDecisions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IsHeld = true,
            Reason = MemberRiskHighRewardHoldConsumer.Reason,
            DecidedByUserId = (Guid?)null
        });

        (await PublishingCodeAsync(auth)).Should().Be("HEALTH_RESTRICTED");
    }

    [Fact]
    public async Task Either_kind_of_evidence_alone_holds_nothing()
    {
        var (rejected, _) = await MemberAsync();
        var (reported, _) = await MemberAsync();

        await AnnounceAsync(Rejection(rejected, "FACE_MATCH_FAILED"));
        await RemovalsStandAsync(Removal(reported), Removal(reported), Removal(reported));

        (await RiskOfAsync(rejected)).Level.Should().Be(RiskLevel.Medium);
        (await RiskOfAsync(reported)).Level.Should().Be(RiskLevel.Medium);
        ((await AccountOfAsync(rejected))?.IsConversionBlocked ?? false).Should().BeFalse();
        ((await AccountOfAsync(reported))?.IsConversionBlocked ?? false).Should().BeFalse();
    }

    [Fact]
    public async Task An_honest_rejection_and_three_reports_on_one_post_count_for_nothing()
    {
        var (member, _) = await MemberAsync();
        var post = Guid.NewGuid();

        await AnnounceAsync(Rejection(member, "ILLEGIBLE_DOCUMENT"));
        await RemovalsStandAsync(Removal(member, post), Removal(member, post), Removal(member, post));

        (await RiskOfAsync(member)).Should().Match<RiskAssessment>(risk =>
            risk.Level == RiskLevel.Low && risk.Score == RiskAssessment.BaseScore);

        var evidence = await EvidenceOfAsync(member);
        evidence.Should().HaveCount(3).And.OnlyContain(signal => signal.Kind == RiskSignalKind.ReportUpheld,
            "an honest rejection stores nothing");
    }

    [Fact]
    public async Task A_report_delivered_three_times_counts_once()
    {
        var (member, _) = await MemberAsync();
        var report = Removal(member);

        // One report's removal recorded three times, each in its own transaction, as a consequence
        // run retried after a failed save would record it.
        await AnnounceAsync(Rejection(member, "DUPLICATE_SUBMISSION"));
        await RemovalsStandAsync(report, report, report);

        (await RiskOfAsync(member)).Level.Should().Be(RiskLevel.Medium);
        (await EvidenceOfAsync(member)).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_sweep_lowers_a_stale_assessment_and_the_hold_stays_for_staff()
    {
        var (member, auth) = await MemberAsync();

        // A member assessed High on evidence that has since aged out: an assessment on record and no
        // evidence behind it, as the sweep finds a member a month later.
        using (var scope = Factory.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<TrustProfileProvisioner>()
                .GetOrCreateAsync(UserId.From(member));
            profile.UpdateRisk(RiskAssessment.From(
                [RiskFactor.Failed_Verification, RiskFactor.Reports_Spike], DateTimeOffset.UtcNow));

            await scope.ServiceProvider.GetRequiredService<IRiskSignalRepository>().AddAsync(
                RiskSignal.ReportUpheld(
                    UserId.From(member), "Jobs", "job", Guid.NewGuid(), Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddDays(-40)));

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await DrainAsync();
        (await AccountOfAsync(member))!.IsConversionBlocked.Should().BeTrue();
        (await PublishingCodeAsync(auth)).Should().Be("HEALTH_RESTRICTED");

        await Factory.Services.GetServices<IHostedService>().OfType<TrustRiskSweep>().Single()
            .RunOnceAsync(CancellationToken.None);
        await DrainAsync();

        (await RiskOfAsync(member)).Level.Should().Be(RiskLevel.Low);
        (await EvidenceOfAsync(member)).Should().BeEmpty("evidence past the longest window is removed");
        (await AccountOfAsync(member))!.IsConversionBlocked.Should().BeTrue(
            "a level that falls is not a review: staff lift the hold");
        (await PublishingCodeAsync(auth)).Should().NotBe("HEALTH_RESTRICTED");
    }

    private async Task<(Guid Member, AuthenticationResponse Auth)> MemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();

        return (auth.User.Id, auth);
    }

    private static VerificationRejectedIntegrationEvent Rejection(Guid member, string reasonCode) => new()
    {
        VerificationId = Guid.NewGuid(),
        UserId = member,
        VerificationType = "Identity",
        ReasonCode = reasonCode,
        RejectedAt = DateTimeOffset.UtcNow
    };

    /// <summary>A reported job of the owner's whose removal stood.</summary>
    private static (Guid Owner, Guid ContentId, Guid ReportId) Removal(Guid owner, Guid? contentId = null) =>
        (owner, contentId ?? Guid.NewGuid(), Guid.NewGuid());

    /// <summary>
    /// Records each removal against its owner as <c>ModerationConsequences</c> does once it stands,
    /// each in its own transaction, then delivers the level Trust announces and the hold.
    /// </summary>
    private async Task RemovalsStandAsync(params (Guid Owner, Guid ContentId, Guid ReportId)[] removals)
    {
        foreach (var (owner, contentId, reportId) in removals)
        {
            using var scope = Factory.Services.CreateScope();

            await scope.ServiceProvider.GetRequiredService<MemberRiskAssessor>().RecordAsync(
                RiskSignal.ReportUpheld(UserId.From(owner), "Jobs", "job", contentId, reportId, DateTimeOffset.UtcNow),
                CancellationToken.None);

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await DrainAsync();
    }

    /// <summary>Publishes through the outbox as their modules would, and delivers everything that follows.</summary>
    private async Task AnnounceAsync(params Diaspora.Contracts.Events.IIntegrationEvent[] announcements)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            foreach (var announcement in announcements)
            {
                await publisher.PublishAsync(announcement);
            }

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await DrainAsync();
    }

    /// <summary>Evidence, then the level Trust announces from it, then the hold.</summary>
    private async Task DrainAsync()
    {
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();
    }

    private async Task<RiskAssessment> RiskOfAsync(Guid member)
    {
        using var scope = Factory.Services.CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<ITrustProfileRepository>()
            .GetByUserReadOnlyAsync(UserId.From(member)))!.Risk;
    }

    private async Task<IReadOnlyList<RiskSignal>> EvidenceOfAsync(Guid member)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IRiskSignalRepository>()
            .ListSinceAsync(UserId.From(member), DateTimeOffset.UnixEpoch);
    }

    private async Task<RewardAccount?> AccountOfAsync(Guid member)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(member);
    }

    /// <summary>What Trust answers when the member asks to post a job, a publishing capability High pauses.</summary>
    private async Task<string> PublishingCodeAsync(AuthenticationResponse auth)
    {
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/eligibility", new { capability = "jobs", dryRun = true }, JsonOptions);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        return body.GetProperty("data").GetProperty("code").GetString()!;
    }
}
