using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Extensibility.Handlers;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The two kernel ports Trust answers for the rest of the platform.
/// <para>
/// <c>ITrustService</c> has been Trust's since ADR-002. <c>IEligibilityService</c> was not — the
/// kernel's placeholder returned <c>Eligible</c> for every action for every member while this module
/// held the real evaluator and served it over HTTP. PSR-1.2 joined the second half; these assert
/// what the port now says.
/// </para>
/// </summary>
public sealed class TrustPlatformPortTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member =
        UserId.From(Guid.Parse("44444444-4444-4444-4444-444444444444"));

    // ── Account standing ─────────────────────────────────────────────────

    /// <summary>
    /// Five rungs project onto four. <c>Excellent</c> and <c>Good</c> are one answer outside this
    /// module: the distinction between them is a reputation signal, not a restriction, and a
    /// consumer that could see it would eventually branch on it.
    /// </summary>
    [Theory]
    [InlineData(AccountHealth.Excellent, AccountStanding.Good)]
    [InlineData(AccountHealth.Good, AccountStanding.Good)]
    [InlineData(AccountHealth.Restricted, AccountStanding.Restricted)]
    [InlineData(AccountHealth.Suspended, AccountStanding.Suspended)]
    [InlineData(AccountHealth.UnderReview, AccountStanding.UnderReview)]
    public void Account_health_projects_onto_the_kernels_standing(
        AccountHealth health, AccountStanding expected)
    {
        TrustEngineService.ToPlatformStanding(health).Should().Be(expected);
    }

    [Fact]
    public async Task A_member_with_no_profile_has_an_unknown_standing_rather_than_a_bad_one()
    {
        var profiles = Substitute.For<ITrustProfileRepository>();
        profiles.GetByUserReadOnlyAsync(Member, Arg.Any<CancellationToken>())
            .Returns((TrustProfile?)null);

        var service = new TrustEngineService(profiles, Clock());

        // Everybody is in this state between registering and being provisioned. Answering Good
        // would assert something nothing has recorded; answering a restriction would refuse them.
        (await service.GetAccountStandingAsync(Member)).Should().Be(AccountStanding.Unknown);
    }

    [Fact]
    public async Task A_suspended_profile_reports_a_suspension_through_the_kernel_port()
    {
        var profile = TrustProfile.Open(Member, Now);
        profile.Suspend("Community Guidelines.", Now);

        var profiles = Substitute.For<ITrustProfileRepository>();
        profiles.GetByUserReadOnlyAsync(Member, Arg.Any<CancellationToken>()).Returns(profile);

        var service = new TrustEngineService(profiles, Clock());

        (await service.GetAccountStandingAsync(Member)).Should().Be(AccountStanding.Suspended);
    }

    /// <summary>
    /// The kernel's placeholder is deliberately visibly wrong rather than plausibly wrong. It must
    /// not claim a member is in good standing when nothing has looked.
    /// </summary>
    [Fact]
    public async Task The_kernels_own_default_admits_it_knows_nothing()
    {
        var port = Type.GetType(
            "Diaspora.Infrastructure.Services.DefaultTrustService, Diaspora.Infrastructure");

        port.Should().NotBeNull();

        var service = (ITrustService)Activator.CreateInstance(port!)!;

        (await service.GetAccountStandingAsync(Member)).Should().Be(AccountStanding.Unknown);
    }

    // ── Eligibility ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_unauthenticated_caller_is_not_eligible_for_anything()
    {
        var service = new TrustEligibilityService(Evaluator(profile: null), CurrentUser(null));

        var result = await service.CheckEligibilityAsync(TrustCapabilities.PublishGuide);

        result.Status.Should().Be(EligibilityStatus.NotEligible);
        result.ReasonCodes.Should().ContainSingle().Which.Should().Be("UNAUTHENTICATED");
    }

    /// <summary>
    /// The evaluator's own documented position, now reachable through the kernel: a capability no
    /// module has declared a requirement for has not asked Trust to gate it.
    /// </summary>
    [Fact]
    public async Task An_undeclared_capability_is_permitted()
    {
        var service = new TrustEligibilityService(
            Evaluator(TrustProfile.Open(Member, Now)), CurrentUser(Member));

        (await service.CheckEligibilityAsync("something.nobody.declared")).Status
            .Should().Be(EligibilityStatus.Eligible);
    }

    /// <summary>
    /// <b>A health restriction is <c>Restricted</c>, not <c>NotEligible</c>, and the difference is
    /// what a caller can do about it.</b> A restriction is imposed and lifts when the platform lifts
    /// it; a threshold is something the member can work towards, which is why only the second
    /// carries required actions. Collapsing them would lose "you may not" against "you cannot yet".
    /// </summary>
    [Fact]
    public async Task A_suspension_comes_back_as_restricted_with_no_checklist_to_work_through()
    {
        var profile = TrustProfile.Open(Member, Now);
        profile.Suspend("Community Guidelines.", Now);

        var service = new TrustEligibilityService(Evaluator(profile), CurrentUser(Member));

        var result = await service.CheckEligibilityAsync(TrustCapabilities.PublishGuide);

        result.Status.Should().Be(EligibilityStatus.Restricted);
        result.ReasonCodes.Should().ContainSingle().Which.Should().Be("HEALTH_RESTRICTED");
        result.CurrentRestrictions.Should().ContainSingle();
        result.RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unmet_threshold_comes_back_as_a_checklist()
    {
        // An untrusted profile with no grants meets none of guide publishing's thresholds.
        var service = new TrustEligibilityService(
            Evaluator(TrustProfile.Open(Member, Now)), CurrentUser(Member));

        var result = await service.CheckEligibilityAsync(TrustCapabilities.PublishGuide);

        result.Status.Should().Be(EligibilityStatus.NotEligible);
        result.ReasonCodes.Should().ContainSingle().Which.Should().Be("DENIED");
        result.RequiredActions.Should().NotBeEmpty();
    }

    /// <summary>
    /// The port carries an action and no intent, so it cannot know whether the caller is about to
    /// act or merely rendering a button. Spending a member's daily budget on a screen refresh is
    /// the worse of the two errors.
    /// <para>
    /// The capability is contributed rather than borrowed from Trust's own five: every one of those
    /// carries a threshold a fresh profile fails, so the evaluator would refuse before it ever
    /// reached the rate limiter and the assertion would hold for the wrong reason.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Asking_never_spends_the_members_rate_limit_budget()
    {
        var limiter = new CountingRateLimiter();

        var service = new TrustEligibilityService(
            Evaluator(TrustProfile.Open(Member, Now), limiter, [new BudgetedCapabilityContributor()]),
            CurrentUser(Member));

        var result = await service.CheckEligibilityAsync(BudgetedCapabilityContributor.Key);

        result.Status.Should().Be(
            EligibilityStatus.Eligible, "the member meets every threshold this capability declares");

        limiter.Acquisitions.Should().Be(
            0, "a question about a capability is not the taking of it");
    }

    // ── Fixtures ─────────────────────────────────────────────────────────

    private static EligibilityEvaluator Evaluator(
        TrustProfile? profile,
        ISubjectRateLimiter? limiter = null,
        IEnumerable<ITrustRequirementContributor>? contributors = null)
    {
        var profiles = Substitute.For<ITrustProfileRepository>();
        profiles.GetByUserReadOnlyAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(profile);

        var reputations = Substitute.For<IReputationProfileRepository>();
        reputations.GetByUserReadOnlyAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((ReputationProfile?)null);

        return new EligibilityEvaluator(
            profiles,
            reputations,
            new TrustRequirementCatalog(contributors ?? []),
            new TrustRuleChain([new SuspendedAccountRule(), new RestrictedAccountRule()]),
            limiter ?? new CountingRateLimiter(),
            Clock());
    }

    /// <summary>A capability with a daily budget and nothing else to meet.</summary>
    private sealed class BudgetedCapabilityContributor : ITrustRequirementContributor
    {
        public const string Key = "psr-1.2.budgeted-capability";

        public string Module => "PSR-1.2 fixture";

        public IEnumerable<TrustRequirement> GetRequirements() =>
        [
            new()
            {
                CapabilityKey = Key,
                Name = "A capability with a budget and no threshold",
                MinimumTier = TrustTier.Untrusted,
                RateLimitPerDay = 3
            }
        ];
    }

    /// <summary>
    /// Hand-written rather than substituted: the assertion is about a call that must not happen,
    /// and a counter reads better than a negative expectation over a <c>ValueTask</c> method.
    /// </summary>
    private sealed class CountingRateLimiter : ISubjectRateLimiter
    {
        public int Acquisitions { get; private set; }

        public ValueTask<RateLimitLease> AcquireAsync(
            string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default)
        {
            Acquisitions++;
            return ValueTask.FromResult(RateLimitLease.Allowed(permitLimit - 1));
        }

        public ValueTask ResetAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private static ICurrentUser CurrentUser(UserId? userId)
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(userId);
        return current;
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return clock;
    }
}
