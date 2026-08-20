using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Policies;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Recommendations.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// What may never be recommended.
/// <para>
/// <b>These are the tests whose failure would be a privacy or safety incident rather than a quality
/// one.</b> A ranking strategy that scores badly produces a worse page; a policy that stops working
/// shows somebody the seller they blocked, the flat that was let last week, or content they are not
/// entitled to see.
/// </para>
/// </summary>
public class RecommendationPolicyTests
{
    // ── Blocked content ──────────────────────────────────────────────────

    [Fact]
    public void A_blocked_target_is_excluded()
    {
        var rule = new BlockedContentPolicyRule();
        var candidate = RecommendationTestData.Candidate();

        var preference = RecommendationTestData.Preference();
        preference.BlockTarget(
            candidate.Target.TargetType,
            candidate.Target.TargetId,
            RecommendationTestData.Now,
            Guid.NewGuid());

        var decision = rule.Evaluate(
            candidate,
            Context(rule.Key, RecommendationPolicyKind.BlockedContent, preference: preference));

        decision.IsExcluded.Should().BeTrue();
        decision.Reason.Should().NotBeNullOrWhiteSpace(
            "a candidate that vanishes with no recorded cause is the hardest defect here to diagnose");
    }

    [Fact]
    public void A_blocked_author_is_excluded()
    {
        var rule = new BlockedContentPolicyRule();
        var author = UserId.New();
        var candidate = RecommendationTestData.Candidate(owner: author);

        var context = Context(rule.Key, RecommendationPolicyKind.BlockedContent) with
        {
            BlockedAuthorIds = new HashSet<Guid> { author.Value }
        };

        rule.Evaluate(candidate, context).IsExcluded.Should().BeTrue();
    }

    // ── Self-authored ────────────────────────────────────────────────────

    /// <summary>
    /// <b>The oldest bug in the genre.</b> Everything a member wrote scores well: they engaged with
    /// it, it matches their interests, it is in their city. Every strategy independently concludes it
    /// is the best candidate on the platform for them, and each is right.
    /// </summary>
    [Fact]
    public void A_member_is_never_recommended_their_own_listing()
    {
        var rule = new SelfAuthoredPolicyRule();
        var owner = UserId.New();
        var candidate = RecommendationTestData.Candidate(owner: owner);

        var profile = RecommendationTestData.Profile(owner);

        var decision = rule.Evaluate(
            candidate,
            Context(rule.Key, RecommendationPolicyKind.SelfAuthored, profile: profile));

        decision.IsExcluded.Should().BeTrue();
    }

    // ── Availability ─────────────────────────────────────────────────────

    [Fact]
    public void An_expired_candidate_is_excluded()
    {
        var rule = new AvailabilityPolicyRule();

        var candidate = RecommendationTestData.Candidate(
            expiresAt: RecommendationTestData.Now.AddHours(-1));

        rule.Evaluate(candidate, Context(rule.Key, RecommendationPolicyKind.Availability))
            .IsExcluded.Should().BeTrue();
    }

    [Fact]
    public void A_withdrawn_candidate_is_excluded()
    {
        var rule = new AvailabilityPolicyRule();
        var candidate = RecommendationTestData.Candidate();

        candidate.Withdraw();

        rule.Evaluate(candidate, Context(rule.Key, RecommendationPolicyKind.Availability))
            .IsExcluded.Should().BeTrue();
    }

    // ── Trust ────────────────────────────────────────────────────────────

    [Fact]
    public void Verified_only_content_is_withheld_from_an_unverified_member()
    {
        var rule = new TrustRestrictionPolicyRule();

        var candidate = RecommendationTestData.Candidate(
            visibility: CandidateVisibility.Verified);

        var unverified = RecommendationTestData.Profile(isVerified: false);
        var verified = RecommendationTestData.Profile(isVerified: true);

        rule.Evaluate(
                candidate,
                Context(rule.Key, RecommendationPolicyKind.TrustRestriction, profile: unverified))
            .IsExcluded.Should().BeTrue();

        rule.Evaluate(
                candidate,
                Context(rule.Key, RecommendationPolicyKind.TrustRestriction, profile: verified))
            .IsExcluded.Should().BeFalse();
    }

    // ── Country visibility ───────────────────────────────────────────────

    [Fact]
    public void Country_restricted_content_is_withheld_from_another_country()
    {
        var rule = new CountryVisibilityPolicyRule();

        var candidate = RecommendationTestData.Candidate(
            countryCode: "NG", visibility: CandidateVisibility.CountryOnly);

        var decision = rule.Evaluate(
            candidate,
            Context(
                rule.Key,
                RecommendationPolicyKind.CountryVisibility,
                profile: RecommendationTestData.Profile(countryCode: "MU")));

        decision.IsExcluded.Should().BeTrue();
    }

    /// <summary>
    /// <b>The permissive reading is the default and the strict one is a parameter.</b> A member
    /// mid-onboarding has no country, and excluding country-restricted content from them would empty
    /// the feed for exactly the members who most need it. A deployment whose legal position is
    /// stricter sets the parameter.
    /// </summary>
    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void An_unknown_country_is_governed_by_the_strictness_parameter(
        string requireCountry, bool expectExcluded)
    {
        var rule = new CountryVisibilityPolicyRule();

        var candidate = RecommendationTestData.Candidate(
            countryCode: "NG", visibility: CandidateVisibility.CountryOnly);

        var policy = Policy(rule.Key, RecommendationPolicyKind.CountryVisibility);
        policy.SetParameter(
            CountryVisibilityPolicyRule.RequireCountryParameter, requireCountry, Guid.NewGuid());

        var decision = rule.Evaluate(
            candidate,
            new PolicyContext
            {
                Personalization = RecommendationTestData.Context(
                    RecommendationTestData.Profile(countryCode: null)),
                Configuration = policy
            });

        decision.IsExcluded.Should().Be(expectExcluded);
    }

    // ── Muted categories ─────────────────────────────────────────────────

    [Fact]
    public void A_muted_category_is_excluded()
    {
        var rule = new MutedCategoryPolicyRule();
        var candidate = RecommendationTestData.Candidate(categoryKey: "politics");

        var preference = RecommendationTestData.Preference();
        preference.SetCategory("politics", isMuted: true, targetType: null, Guid.NewGuid());

        rule.Evaluate(
                candidate,
                Context(rule.Key, RecommendationPolicyKind.MutedCategory, preference: preference))
            .IsExcluded.Should().BeTrue();
    }

    /// <summary>
    /// A candidate with no category is never muted. Excluding uncategorised content because it might
    /// belong to a muted category would remove most of the platform's cross-cutting guides.
    /// </summary>
    [Fact]
    public void An_uncategorised_candidate_is_never_muted()
    {
        var rule = new MutedCategoryPolicyRule();
        var candidate = RecommendationTestData.Candidate(categoryKey: null);

        var preference = RecommendationTestData.Preference();
        preference.SetCategory("politics", isMuted: true, targetType: null, Guid.NewGuid());

        rule.Evaluate(
                candidate,
                Context(rule.Key, RecommendationPolicyKind.MutedCategory, preference: preference))
            .IsExcluded.Should().BeFalse();
    }

    // ── Dismissal ────────────────────────────────────────────────────────

    [Fact]
    public void A_dismissed_target_is_suppressed()
    {
        var rule = new DismissalPolicyRule();
        var candidate = RecommendationTestData.Candidate();

        var context = Context(rule.Key, RecommendationPolicyKind.Dismissal) with
        {
            SuppressedTargetKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                candidate.Target.Key
            }
        };

        rule.Evaluate(candidate, context).IsExcluded.Should().BeTrue();
    }

    // ── Access tier ──────────────────────────────────────────────────────

    [Fact]
    public void A_signed_out_caller_sees_public_content_only()
    {
        var rule = new AccessTierPolicyRule();

        var context = Context(rule.Key, RecommendationPolicyKind.AccessTier, tier: AccessTier.Anonymous);

        rule.Evaluate(
                RecommendationTestData.Candidate(visibility: CandidateVisibility.Members), context)
            .IsExcluded.Should().BeTrue();

        rule.Evaluate(
                RecommendationTestData.Candidate(visibility: CandidateVisibility.Public), context)
            .IsExcluded.Should().BeFalse();
    }

    // ── Onboarding ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>A demotion, not an exclusion, and the choice took some thought.</b> A member who cannot yet
    /// apply for a job should still see that the platform has jobs — that is what tells them what
    /// finishing onboarding is for.
    /// </summary>
    [Fact]
    public void An_unfinished_member_is_shown_jobs_scored_down_rather_than_hidden()
    {
        var rule = new OnboardingStatePolicyRule();

        var policy = Policy(rule.Key, RecommendationPolicyKind.OnboardingState, PolicyEffect.Demote);
        policy.SetDemotionFactor(0.35m, RecommendationTestData.Now);

        var decision = rule.Evaluate(
            RecommendationTestData.Candidate(RecommendationTargetType.Job),
            new PolicyContext
            {
                Personalization = RecommendationTestData.Context(
                    RecommendationTestData.Profile(onboardingComplete: false)),
                Configuration = policy
            });

        decision.IsExcluded.Should().BeFalse("hiding it would hide what finishing onboarding unlocks");
        decision.Factor.Should().Be(0.35m);
    }

    [Fact]
    public void An_unfinished_member_sees_guides_untouched()
    {
        var rule = new OnboardingStatePolicyRule();

        var decision = rule.Evaluate(
            RecommendationTestData.Candidate(RecommendationTargetType.Guide),
            Context(
                rule.Key,
                RecommendationPolicyKind.OnboardingState,
                profile: RecommendationTestData.Profile(onboardingComplete: false)));

        decision.Factor.Should().Be(1m, "guides are how somebody finishes onboarding");
    }

    // ── The shape of a decision ──────────────────────────────────────────

    /// <summary>
    /// <b>A demotion can never be an exclusion in disguise.</b> Zero would remove a candidate from
    /// the page while reporting itself in the diagnostics as a nudge, which is exactly the ambiguity
    /// the three-outcome shape exists to prevent.
    /// </summary>
    [Fact]
    public void A_demotion_is_clamped_above_zero()
    {
        PolicyDecision.Demote(0m, "test").Factor.Should().BeGreaterThan(0m);
        PolicyDecision.Demote(-5m, "test").Factor.Should().BeGreaterThan(0m);
    }

    /// <summary>An exclusion always carries a reason. Enforced by the factory, not by convention.</summary>
    [Fact]
    public void An_exclusion_requires_a_reason()
    {
        var act = () => PolicyDecision.Exclude("   ");

        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static RecommendationPolicy Policy(
        string key,
        RecommendationPolicyKind kind,
        PolicyEffect effect = PolicyEffect.Exclude) =>
        RecommendationPolicy.Define(
            RecommendationPolicyId.New(),
            key,
            kind,
            effect,
            "Test policy",
            RecommendationTestData.Now);

    private static PolicyContext Context(
        string key,
        RecommendationPolicyKind kind,
        Modules.Recommendations.Domain.Profiles.PersonalizationProfile? profile = null,
        Modules.Recommendations.Domain.Preferences.PersonalizationPreference? preference = null,
        AccessTier tier = AccessTier.Limited) =>
        new()
        {
            Personalization = RecommendationTestData.Context(profile, preference, tier: tier),
            Configuration = Policy(key, kind)
        };
}
