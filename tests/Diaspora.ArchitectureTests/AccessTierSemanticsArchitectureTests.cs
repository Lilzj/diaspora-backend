using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The canonical access-tier semantics, enforced across every module at once.
/// <para>
/// B2.0.1 exists because the platform held two incompatible readings of
/// <see cref="AccessTier.Onboarding"/> and nothing could tell. These tests are the mechanism
/// that makes a third reading impossible: they run over the <b>real, composed catalogues</b>
/// from every registered contributor, not over a fixture, so a module that drifts fails the
/// build rather than the next phase's investigation.
/// </para>
/// <para>
/// The two that carry the most weight are
/// <see cref="The_onboarding_tier_admits_exactly_these_capabilities_and_no_others"/> — an
/// explicit, reviewed allow-list, so adding an onboarding-tier capability requires amending the
/// specification — and
/// <see cref="No_onboarding_capability_is_a_membership_privilege"/>, which catches the specific
/// mistake this patch exists to prevent.
/// </para>
/// </summary>
public class AccessTierSemanticsArchitectureTests
{
    private static readonly IReadOnlyList<PermissionDefinition> AllPermissions =
        Instantiate<IPermissionContributor>()
            .SelectMany(contributor => contributor.GetPermissions())
            .ToList();

    private static readonly IReadOnlyList<FeatureDefinition> AllFeatures =
        Instantiate<IFeatureContributor>()
            .SelectMany(contributor => contributor.GetFeatures())
            .ToList();

    /// <summary>
    /// Every capability the platform opens at <see cref="AccessTier.Onboarding"/>.
    /// <para>
    /// Reviewed line by line in B2.0.1 and recorded in the feature gate matrix. Each one is here
    /// because withholding it would make onboarding impossible or create a loop with no
    /// entrance — a member must be able to read the country catalogue to choose a country, see
    /// the photograph they just picked, and add a profile photograph that the reputation model
    /// scores.
    /// </para>
    /// <para>
    /// <b>Adding to this list is a specification change, not a code change.</b> That is the
    /// point of asserting the exact set rather than a property of it.
    /// </para>
    /// </summary>
    private static readonly string[] SanctionedOnboardingPermissions =
    [
        "identity:profile:read",       // the restriction screen must be able to name you
        "location:catalog:read",       // registration asks for a country of origin
        "location:search",             // and lets you search for it
        "media:read",                  // see the photograph you just picked
        "media:signed-access",          // and the verification document you just uploaded
        "media:upload:image",          // profile photograph — the reputation model scores it
        "media:upload:document",       // verification evidence

        // B13. An account halfway through onboarding is the one that most needs to be reachable:
        // the message telling it how to finish, and the security alert if somebody else is in it.
        // Withholding these is the loop with no entrance the admission test names.
        "notifications:read",
        "notifications:manage",
        "notifications.preferences:read",
        "notifications.preferences:manage",

        // B14. A member with no history is precisely who the cold-start path exists for, so the
        // recommendations that explain the platform have to reach them before onboarding finishes —
        // and the controls over those recommendations have to be available from the same moment,
        // because a privacy switch that opens later than the thing it switches off is not a switch.
        "recommendations:read",
        "recommendations.signals:record",
        "recommendations.profile:read",
        "recommendations.profile:manage",
        "recommendations.preferences:read",
        "recommendations.preferences:manage",

        // B15. A financial identity is opened the moment somebody registers, so a welcome bonus or
        // a referral credit can land before verification — and a member who has been granted money
        // must be able to see it. This is the only financial permission below Verified: the other
        // four reads describe payments, purchases, subscriptions and giving, none of which can exist
        // at this tier, so opening them here would grant the right to read an empty list.
        "financial:wallet:read",
        "recommendations.feedback:record",

        // B18.x. Reading the creator programme is how a member decides whether to join it, and the
        // Phase 7 register states the floor outright: "creator:profile:read from Onboarding". A
        // ladder somebody cannot see until onboarding finishes is a ladder they will not climb, and
        // this read grants nothing over anybody else — it shows a status, a set of requirements and
        // a public directory. Every act on that ladder is Limited or above.
        "creator:profile:read",

        // B19. A member is instrumented from the first screen they see, and the screens that most
        // need measuring are the onboarding ones — a funnel that started at Limited would be blind
        // to precisely the steps somebody drops out of. It is a write, and it is still not a
        // membership privilege: it reports what the caller did in their own app, reaches nobody
        // else, and the server attributes it to them rather than accepting an identity. Reading the
        // figures it produces is analytics:creator:read at Limited.
        "analytics:telemetry:write"
    ];

    private static readonly string[] SanctionedOnboardingFeatures =
    [
        "media.upload",
        "media.upload.photo",
        "media.upload.document",
        "media.upload.resumable",
        "media.signed-urls",

        // B13. See the permission list above: the inbox is how onboarding is explained.
        "notifications.inbox",

        // B14. See the permission list above.
        "recommendations.view",
        "recommendations.personalization",
        "recommendations.signals",

        // B15. See the permission list above. Seeing a balance somebody was granted is the whole of
        // what this gate opens; paying, buying, subscribing and giving are all Verified.
        "financial.wallet",

        // B18.x. The two read-only halves of the creator programme: what the ladder is, and who is
        // on it. Both front creator:profile:read and open nothing anybody can act with — taking
        // part is creator.challenges, creator.grants, creator.academy, creator.referrals and
        // creator.rewards, every one of which is Limited.
        "creator.programme",
        "creator.discovery",

        // B19. See the permission list above: instrumentation begins at the first screen. The two
        // gates that open the dashboards — analytics.creator and analytics.content — are Limited.
        "analytics.telemetry"
    ];

    // ── Totality ─────────────────────────────────────────────────────────

    [Fact]
    public void Every_permission_classifies_into_exactly_one_capability_category()
    {
        AllPermissions.Should().NotBeEmpty();

        foreach (var permission in AllPermissions)
        {
            var act = () => CapabilityClassifier.Classify(permission);

            act.Should().NotThrow($"'{permission.Key}' must have a documented capability category");
        }
    }

    [Fact]
    public void Every_feature_gate_classifies_into_exactly_one_capability_category()
    {
        AllFeatures.Should().NotBeEmpty();

        foreach (var feature in AllFeatures)
        {
            var act = () => CapabilityClassifier.Classify(feature);

            act.Should().NotThrow($"'{feature.Name}' must have a documented capability category");
        }
    }

    [Fact]
    public void Every_feature_gate_declares_a_tier_consistent_with_its_category()
    {
        var offenders = AllFeatures
            .Where(feature => !CapabilityClassifier.TierMatchesCategory(
                feature.MinimumTier, CapabilityClassifier.Classify(feature)))
            .Select(feature => $"{feature.Name} ({feature.MinimumTier})")
            .ToList();

        offenders.Should().BeEmpty(
            "a gate whose tier contradicts its category is exactly the ambiguity B2.0.1 removed");
    }

    [Fact]
    public void Every_permission_declares_a_tier_consistent_with_its_category()
    {
        var offenders = AllPermissions
            .Where(permission => permission.MinimumTier is not null)
            .Where(permission => !CapabilityClassifier.TierMatchesCategory(
                permission.MinimumTier!.Value, CapabilityClassifier.Classify(permission)))
            .Select(permission => $"{permission.Key} ({permission.MinimumTier})")
            .ToList();

        offenders.Should().BeEmpty();
    }

    // ── The onboarding rung ──────────────────────────────────────────────

    [Fact]
    public void The_onboarding_tier_admits_exactly_these_capabilities_and_no_others()
    {
        AllPermissions
            .Where(permission => permission.MinimumTier == AccessTier.Onboarding)
            .Select(permission => permission.Key)
            .Should().BeEquivalentTo(
                SanctionedOnboardingPermissions,
                "an onboarding-tier capability is a specification decision — amend "
                + "docs/identity/access-tier-specification.md and this list together");

        AllFeatures
            .Where(feature => feature.MinimumTier == AccessTier.Onboarding)
            .Select(feature => feature.Name)
            .Should().BeEquivalentTo(SanctionedOnboardingFeatures);
    }

    [Fact]
    public void No_onboarding_capability_is_a_membership_privilege()
    {
        // The mistake this patch exists to prevent, caught on the name before anybody has to
        // reason about intent.
        var offenders = AllPermissions
            .Where(permission => permission.MinimumTier == AccessTier.Onboarding)
            .Select(permission => permission.Key)
            .Concat(AllFeatures
                .Where(feature => feature.MinimumTier == AccessTier.Onboarding)
                .Select(feature => feature.Name))
            .Where(CapabilityClassifier.ReadsAsMembershipPrivilege)
            .ToList();

        offenders.Should().BeEmpty(
            "reading the community, commenting, reacting, following, messaging and publishing "
            + "are membership privileges and never open before onboarding is complete");
    }

    [Fact]
    public void Every_onboarding_capability_classifies_as_an_onboarding_capability()
    {
        AllFeatures
            .Where(feature => feature.MinimumTier == AccessTier.Onboarding)
            .Should().OnlyContain(feature =>
                CapabilityClassifier.Classify(feature) == CapabilityCategory.Onboarding);
    }

    // ── The membership rung ──────────────────────────────────────────────

    [Fact]
    public void Community_participation_never_opens_before_limited()
    {
        // The floor B6 chose deliberately, now pinned by the specification rather than by that
        // module's own test alone.
        AllFeatures
            .Where(feature => feature.Name.StartsWith("community.", StringComparison.Ordinal))
            .Should().OnlyContain(feature => feature.MinimumTier >= AccessTier.Limited);

        AllPermissions
            .Where(permission => permission.Key.StartsWith("community:", StringComparison.Ordinal))
            .Should().OnlyContain(permission => permission.MinimumTier >= AccessTier.Limited);
    }

    [Fact]
    public void Pending_verification_holds_exactly_what_limited_holds()
    {
        // Waiting on the Trust Engine is neither a promotion nor a demotion. A capability that
        // stops applying once a request is in flight says so with MaximumTier.
        AccessTierSemantics.AdmissibleCategories(AccessTier.PendingVerification)
            .Should().BeEquivalentTo(
                AccessTierSemantics.AdmissibleCategories(AccessTier.Limited));
    }

    // ── Role scope ───────────────────────────────────────────────────────

    [Fact]
    public void Every_administrative_capability_is_reached_by_role_or_by_service_token()
    {
        var administrative = AllPermissions
            .Where(permission =>
                CapabilityClassifier.Classify(permission) == CapabilityCategory.Administrative)
            .ToList();

        administrative.Should().NotBeEmpty();

        administrative.Should().OnlyContain(permission =>
            permission.Roles.Count > 0
            || permission.Audience == PermissionAudience.Service
            || permission.MinimumTier == null);
    }

    [Fact]
    public void No_tier_admits_a_role_scoped_category_on_progression_alone()
    {
        // Asking "does Limited admit administrative capabilities" is the conflation this patch
        // removes: progression and privilege are different axes.
        foreach (var tier in Enum.GetValues<AccessTier>())
        {
            AccessTierSemantics.Admits(tier, CapabilityCategory.Administrative)
                .Should().BeFalse();

            AccessTierSemantics.Admits(tier, CapabilityCategory.Contributor)
                .Should().BeFalse();
        }
    }

    [Fact]
    public void No_member_permission_is_unreachable_by_every_tier_and_every_role()
    {
        // A permission with no tier, no role and a member audience can never be held. None
        // exists; this stops one appearing.
        AllPermissions
            .Where(permission => permission.Audience == PermissionAudience.Member)
            .Should().OnlyContain(permission =>
                permission.MinimumTier != null || permission.Roles.Count > 0);
    }

    // ── A discrepancy B2.0.1 found and recorded ──────────────────────────

    /// <summary>
    /// Gate names that describe staff work. A gate matching one of these should be
    /// <c>StaffOnly</c>, so the client renders a locked card instead of a button that 403s.
    /// </summary>
    private static readonly string[] StaffVocabulary =
        ["moderation", "moderate", "administration", "queue"];

    /// <summary>
    /// The two gates that describe staff work without declaring <c>StaffOnly</c>.
    /// <para>
    /// <b>A real discrepancy, found by B2.0.1 and deliberately not fixed by it.</b> Both sit at
    /// <see cref="AccessTier.Verified"/> while the permissions behind them
    /// (<c>community:moderation:read</c>, <c>trust:verification:review</c>) are restricted to
    /// Moderator, Admin and SuperAdmin. The route is safe — the permission refuses — but a
    /// verified non-staff member is shown a control that will 403, which is precisely what
    /// feature gates exist to prevent.
    /// </para>
    /// <para>
    /// Fixing it means editing Trust and Community production code, which this patch is
    /// forbidden to do. It is recorded in the compatibility report as a finding for those
    /// modules' owners. This list exists so a <em>third</em> one cannot appear unnoticed.
    /// </para>
    /// </summary>
    private static readonly string[] KnownUnflaggedStaffGates =
        ["community.moderation-queue", "trust.moderation.queue"];

    [Fact]
    public void A_gate_describing_staff_work_declares_staff_only()
    {
        var unflagged = AllFeatures
            .Where(feature => !feature.StaffOnly)
            .Where(feature => feature.Name
                .Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => StaffVocabulary.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .Select(feature => feature.Name)
            .ToList();

        unflagged.Should().BeEquivalentTo(
            KnownUnflaggedStaffGates,
            "these two are recorded findings; a third means a module has invented a new way to "
            + "show a member a control that will refuse them");
    }

    // ── Documentation is not optional ────────────────────────────────────

    [Fact]
    public void Every_tier_has_a_semantic_definition_and_a_transition_rule()
    {
        foreach (var tier in Enum.GetValues<AccessTier>())
        {
            AccessTierSemantics.Describe(tier)
                .Should().NotBe("Unknown tier.", $"{tier} must be specified");

            AccessTierSemantics.DescribeTransition(tier)
                .Should().NotBe("Unknown tier.", $"{tier} must document how a subject leaves it");
        }
    }

    [Fact]
    public void Every_capability_category_has_a_documented_tier_band()
    {
        foreach (var category in Enum.GetValues<CapabilityCategory>())
        {
            var floor = AccessTierSemantics.FloorFor(category);
            var ceiling = AccessTierSemantics.CeilingFor(category);

            // Either both are stated (a progression category) or neither is (role-scoped).
            (floor is null).Should().Be(ceiling is null, $"{category} must be one or the other");

            if (floor is not null)
            {
                ((int)floor.Value).Should().BeLessThanOrEqualTo((int)ceiling!.Value);
            }
        }
    }

    private static List<T> Instantiate<T>() where T : class =>
        ModuleAssemblyScanner
            .FindImplementations<T>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (T)Activator.CreateInstance(type)!)
            .ToList();
}
