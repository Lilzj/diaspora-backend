using Diaspora.Modules.Trust.Application.Services;
using PlatformTrustLevel = Diaspora.Platform.Services.Models.TrustLevel;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Extensibility.Handlers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>Reputation scoring, levelling, and the benefits derived from a level.</summary>
public sealed class ReputationProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static ReputationProfile NewProfile() => ReputationProfile.Open(Member, Now);

    [Fact]
    public void A_new_profile_is_a_member_on_zero_points()
    {
        var profile = NewProfile();

        profile.CombinedScore.Should().Be(0);
        profile.Level.Should().Be(ReputationLevel.Member);
        profile.NextLevel!.Value.Level.Should().Be(ReputationLevel.VerifiedResident);
    }

    [Fact]
    public void Scoring_moves_the_dimension_and_appends_an_event()
    {
        var profile = NewProfile();

        profile.Apply(
            ReputationAward.Of(ReputationActions.GuidePublished, ReputationDimension.contribution, 50),
            "guide", "guide-1", "GB", Now);

        profile.ContributionScore.Should().Be(50);
        profile.GuidesPublished.Should().Be(1);
        profile.Events.Should().ContainSingle();
    }

    [Fact]
    public void Crossing_a_threshold_changes_the_level()
    {
        var profile = NewProfile();

        profile.Apply(
            ReputationAward.Of("Bulk", ReputationDimension.contribution, ReputationThresholds.VerifiedResident),
            null, null, null, Now);

        profile.Level.Should().Be(ReputationLevel.VerifiedResident);
        profile.LevelChangedAt.Should().Be(Now);
    }

    /// <summary>
    /// The only dimension that can fall. A violation costs roughly a guide and a half —
    /// enough to matter, not enough to erase a good record in one strike.
    /// </summary>
    [Fact]
    public void A_violation_subtracts_and_restarts_the_clean_streak()
    {
        var profile = NewProfile();
        profile.Apply(
            ReputationAward.Of("Seed", ReputationDimension.trust, 200), null, null, null, Now);

        profile.Apply(
            ReputationAward.Of(ReputationActions.ViolationRecorded, ReputationDimension.trust, -75),
            null, null, null, Now.AddDays(30));

        profile.TrustScore.Should().Be(125);
        profile.ViolationCount.Should().Be(1);
        profile.NoViolationStreakDays(Now.AddDays(40)).Should().Be(10,
            "the streak restarts at the violation, not at signup");
    }

    [Fact]
    public void Scores_never_go_negative()
    {
        var profile = NewProfile();

        profile.Apply(
            ReputationAward.Of(ReputationActions.ViolationRecorded, ReputationDimension.trust, -75),
            null, null, null, Now);

        profile.TrustScore.Should().Be(0);
    }

    [Fact]
    public void Verification_flags_are_set_by_their_actions()
    {
        var profile = NewProfile();

        profile.Apply(
            ReputationAward.Of(ReputationActions.PhoneVerified, ReputationDimension.verification, 20),
            null, null, null, Now);

        profile.PhoneVerified.Should().BeTrue();
        profile.VerificationScore.Should().Be(20);
    }

    [Fact]
    public void Benefits_are_derived_from_the_level_not_stored()
    {
        var profile = NewProfile();
        profile.Benefits.CanPublishImmediately.Should().BeFalse();

        profile.Apply(
            ReputationAward.Of("Bulk", ReputationDimension.contribution, ReputationThresholds.CommunityExpert),
            null, null, null, Now);

        profile.Benefits.CanPublishImmediately.Should().BeTrue();
        profile.Benefits.CanModerate.Should().BeTrue();
        profile.Benefits.SearchBoostFactor.Should().BeGreaterThan(1m);
    }

    [Fact]
    public void The_top_of_the_ladder_has_no_next_level()
    {
        var profile = NewProfile();

        profile.Apply(
            ReputationAward.Of("Bulk", ReputationDimension.contribution, ReputationThresholds.CommunityAmbassador),
            null, null, null, Now);

        profile.Level.Should().Be(ReputationLevel.CommunityAmbassador);
        profile.NextLevel.Should().BeNull();
    }

    /// <summary>
    /// The thresholds must match the frontend's <c>REPUTATION_LEVEL_THRESHOLDS</c> exactly.
    /// If they drift, a member is shown a level that does not unlock what it says it does.
    /// </summary>
    [Theory]
    [InlineData(0, ReputationLevel.Member)]
    [InlineData(99, ReputationLevel.Member)]
    [InlineData(100, ReputationLevel.VerifiedResident)]
    [InlineData(350, ReputationLevel.TrustedContributor)]
    [InlineData(1000, ReputationLevel.CommunityExpert)]
    [InlineData(2500, ReputationLevel.CommunityAmbassador)]
    public void Thresholds_match_the_frontend(int score, ReputationLevel expected) =>
        ReputationThresholds.LevelFor(score).Should().Be(expected);
}

/// <summary>
/// The extensibility seams: that they compose, and that they fail loudly on conflict.
/// <para>
/// These are the tests behind the brief's "extensible without modifying existing code". A
/// duplicate registration resolving silently by load order would make every other guarantee
/// in the module conditional on assembly ordering.
/// </para>
/// </summary>
public sealed class TrustExtensibilityTests
{
    [Fact]
    public void Two_handlers_claiming_one_verification_type_fail_at_startup()
    {
        var build = () => new VerificationTypeHandlerRegistry(
            [new DiasporaVerificationHandler(), new DuplicateDiasporaHandler()]);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*Two verification handlers claim 'Diaspora'*");
    }

    [Fact]
    public void The_registry_resolves_a_handler_per_type()
    {
        var registry = new VerificationTypeHandlerRegistry(
            [new DiasporaVerificationHandler(), new IdentityVerificationHandler()]);

        registry.For(VerificationType.Diaspora).Should().BeOfType<DiasporaVerificationHandler>();
        registry.Supports(VerificationType.Business).Should().BeFalse();
    }

    [Fact]
    public void A_contributor_cannot_reprice_an_action_another_module_owns()
    {
        var build = () => new ReputationRuleCatalog([new ConflictingRuleContributor()]);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*already owns*");
    }

    [Fact]
    public void A_contributor_can_add_a_new_action()
    {
        var catalog = new ReputationRuleCatalog([new NewActionContributor()]);

        catalog.For("SaleCompleted").Should().NotBeNull();
        catalog.For(ReputationActions.GuidePublished).Should().NotBeNull(
            "contributed rules add to the defaults rather than replacing them");
    }

    [Fact]
    public void Two_rules_deciding_one_badge_fail_at_startup()
    {
        var build = () => new BadgeRuleCatalog(
            [new EarlyMemberBadgeRule(), new DuplicateEarlyMemberRule()]);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*Two badge rules decide 'EarlyMember'*");
    }

    [Fact]
    public void A_capability_cannot_be_declared_twice()
    {
        var build = () => new TrustRequirementCatalog([new ConflictingRequirementContributor()]);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*already owns*");
    }

    /// <summary>
    /// First refusal wins, so a member sees the most important reason rather than whichever
    /// rule happened to run last.
    /// </summary>
    [Fact]
    public void The_rule_chain_returns_the_first_refusal_in_order()
    {
        var chain = new TrustRuleChain([new SuspendedAccountRule(), new RestrictedAccountRule()]);

        var verdict = chain.Evaluate(new TrustRuleContext
        {
            UserId = Guid.NewGuid(),
            CapabilityKey = TrustCapabilities.PublishGuide,
            Tier = TrustTier.Verified,
            Health = AccountHealth.Suspended,
            Grants = [],
            CombinedScore = 5000,
            ViolationCount = 3,
            RiskLevel = RiskLevel.Low,
            AsOf = DateTimeOffset.UtcNow
        });

        verdict.Allowed.Should().BeFalse();
        verdict.Code.Should().Be(EligibilityCode.HEALTH_RESTRICTED);
        verdict.Reason.Should().Contain("suspended",
            "a suspended member should be told they are suspended, not that they need more points");
    }

    [Fact]
    public void A_rule_that_does_not_apply_to_the_capability_is_skipped()
    {
        var chain = new TrustRuleChain([new RestrictedAccountRule()]);

        // Restricted blocks publishing but not reading — "moderation" is not in its list.
        var verdict = chain.Evaluate(new TrustRuleContext
        {
            UserId = Guid.NewGuid(),
            CapabilityKey = TrustCapabilities.Moderate,
            Tier = TrustTier.Verified,
            Health = AccountHealth.Restricted,
            Grants = [],
            CombinedScore = 0,
            ViolationCount = 1,
            RiskLevel = RiskLevel.Low,
            AsOf = DateTimeOffset.UtcNow
        });

        verdict.Allowed.Should().BeTrue();
    }

    // ── Test doubles ─────────────────────────────────────────────────────

    private sealed class DuplicateDiasporaHandler : VerificationTypeHandlerBase
    {
        public override VerificationType Type => VerificationType.Diaspora;
    }

    private sealed class DuplicateEarlyMemberRule : IBadgeRule
    {
        public BadgeType Badge => BadgeType.EarlyMember;
        public bool IsCountryScoped => false;
        public bool IsEarned(BadgeEvaluationContext context) => true;
    }

    private sealed class ConflictingRuleContributor : IReputationRuleContributor
    {
        public string Module => "Marketplace";

        public IEnumerable<ReputationRule> GetRules()
        {
            yield return new ReputationRule
            {
                ActionType = ReputationActions.GuidePublished,
                Dimension = ReputationDimension.contribution,
                Points = 5000
            };
        }
    }

    private sealed class NewActionContributor : IReputationRuleContributor
    {
        public string Module => "Marketplace";

        public IEnumerable<ReputationRule> GetRules()
        {
            yield return new ReputationRule
            {
                ActionType = "SaleCompleted",
                Dimension = ReputationDimension.trust,
                Points = 10
            };
        }
    }

    private sealed class ConflictingRequirementContributor : ITrustRequirementContributor
    {
        public string Module => "Jobs";

        public IEnumerable<TrustRequirement> GetRequirements()
        {
            yield return new TrustRequirement
            {
                CapabilityKey = TrustCapabilities.PostJob,
                Name = "Job Posting (hijacked)"
            };
        }
    }
}

/// <summary>Risk scoring — weights, banding, and what the band gates.</summary>
public sealed class RiskAssessmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_clean_member_is_low_risk_at_the_base_score()
    {
        var assessment = RiskAssessment.Baseline(Now);

        assessment.Score.Should().Be(RiskAssessment.BaseScore);
        assessment.Level.Should().Be(RiskLevel.Low);
        assessment.WarrantsReview.Should().BeFalse();
    }

    [Fact]
    public void Factors_accumulate_and_band_the_score()
    {
        var assessment = RiskAssessment.From(
            [RiskFactor.Failed_Verification, RiskFactor.Device_Change], Now);

        // 10 base + 25 + 15
        assessment.Score.Should().Be(50);
        assessment.Level.Should().Be(RiskLevel.Medium);
    }

    [Fact]
    public void High_risk_warrants_review()
    {
        var assessment = RiskAssessment.From(
            [RiskFactor.Reports_Spike, RiskFactor.Failed_Verification, RiskFactor.VPN_Detected], Now);

        assessment.Level.Should().Be(RiskLevel.High);
        assessment.WarrantsReview.Should().BeTrue();
    }

    [Fact]
    public void The_score_is_capped_at_one_hundred()
    {
        var assessment = RiskAssessment.From(Enum.GetValues<RiskFactor>(), Now);

        assessment.Score.Should().Be(RiskAssessment.MaxScore);
    }

    [Fact]
    public void Duplicate_factors_are_counted_once()
    {
        var assessment = RiskAssessment.From(
            [RiskFactor.VPN_Detected, RiskFactor.VPN_Detected], Now);

        assessment.Score.Should().Be(30);
        assessment.Factors.Should().ContainSingle();
    }
}

/// <summary>
/// The projection from the module's six-rung tier onto the kernel's four-value contract.
/// </summary>
public sealed class TrustLevelProjectionTests
{
    [Theory]
    [InlineData(TrustTier.Untrusted, PlatformTrustLevel.None)]
    [InlineData(TrustTier.Basic, PlatformTrustLevel.Basic)]
    [InlineData(TrustTier.Provisional, PlatformTrustLevel.Basic)]
    [InlineData(TrustTier.Verified, PlatformTrustLevel.Verified)]
    [InlineData(TrustTier.Trusted, PlatformTrustLevel.Premium)]
    [InlineData(TrustTier.Steward, PlatformTrustLevel.Premium)]
    public void Tiers_project_onto_the_platform_ladder(
        TrustTier tier, PlatformTrustLevel expected) =>
        TrustEngineService.ToPlatformLevel(tier).Should().Be(expected);

    /// <summary>
    /// Provisional maps down rather than across: a member whose permit lapsed still has an
    /// account, but should not be vouching for anything until they re-verify.
    /// </summary>
    [Fact]
    public void Provisional_does_not_read_as_verified() =>
        TrustEngineService.ToPlatformLevel(TrustTier.Provisional)
            .Should().NotBe(PlatformTrustLevel.Verified);
}
