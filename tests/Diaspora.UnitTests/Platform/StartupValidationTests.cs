using Diaspora.Contracts.Authorization;
using Diaspora.Contracts.Enums;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Validation;
using Diaspora.Platform.Validation.Validators;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The boot-time validators, each shown refusing a broken platform and accepting a correct one.
/// <para>
/// <b>The integration suite proves the real platform passes; this proves the rules can fail.</b>
/// Those are different claims, and only the second one survives somebody accidentally deleting a
/// check — a validator that returns nothing passes every green-path assertion ever written about
/// it. Each test below hands a validator a platform with exactly one thing wrong.
/// </para>
/// </summary>
public sealed class StartupValidationTests
{
    // ── The staff-only rule the brief asks to fail a boot over ───────────

    /// <summary>
    /// <b>SV4005.</b> The gate says "render this to everybody"; the permission behind it says "only
    /// moderators may use it". Nothing is exposed — the server refuses correctly — and every member
    /// sees a control that fails when they touch it.
    /// </summary>
    [Fact]
    public void A_gate_open_to_members_over_a_staff_permission_fails_the_boot()
    {
        var findings = Features(
            permissions: new PermissionDefinition(
                "probe:moderation:manage", "Probe", "Moderate.")
            {
                Roles = new HashSet<string>(StringComparer.Ordinal) { nameof(UserRole.Moderator) }
            },
            features: new FeatureDefinition("probe.moderation.manage", "Probe", "Moderate."));

        findings.Should().ContainSingle(finding => finding.Code == "SV4005")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void The_same_gate_declaring_staff_only_is_accepted()
    {
        var findings = Features(
            permissions: new PermissionDefinition(
                "probe:moderation:manage", "Probe", "Moderate.")
            {
                Roles = new HashSet<string>(StringComparer.Ordinal) { nameof(UserRole.Moderator) }
            },
            features: new FeatureDefinition("probe.moderation.manage", "Probe", "Moderate.")
            {
                StaffOnly = true
            });

        findings.Should().BeEmpty();
    }

    /// <summary>
    /// A gate with no permission of the same name is not guessed at. Inferring a relationship would
    /// report the modules whose naming is most careful and miss the ones whose naming is loosest.
    /// </summary>
    [Fact]
    public void A_gate_with_no_matching_permission_is_left_alone()
    {
        Features(
            permissions: new PermissionDefinition("probe:things:read", "Probe", "Read."),
            features: new FeatureDefinition("probe.something.else", "Probe", "Something else."))
            .Should().BeEmpty();
    }

    /// <summary>
    /// <b>SV4004.</b> A ceiling below the floor is a capability the platform believes it offers and
    /// no member at any tier can reach.
    /// </summary>
    [Fact]
    public void A_gate_shut_at_every_tier_fails_the_boot()
    {
        var findings = Features(
            permissions: new PermissionDefinition("probe:things:read", "Probe", "Read."),
            features: new FeatureDefinition("probe.things.read", "Probe", "Read.")
            {
                MinimumTier = AccessTier.Verified,
                MaximumTier = AccessTier.Limited
            });

        findings.Should().ContainSingle(finding => finding.Code == "SV4004")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    // ── Permissions ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>SV3003.</b> No tier grants it and no role names it, so nothing can hold it — as opposed to
    /// the outbox's operator permissions, which name their roles and are held by administrators and
    /// nobody else.
    /// </summary>
    [Fact]
    public void A_permission_no_tier_and_no_role_can_reach_is_reported()
    {
        var findings = Authorization(
            permissions:
            [
                new PermissionDefinition("probe:things:read", "Probe", "Read.")
                {
                    MinimumTier = null
                }
            ],
            policies: []);

        findings.Should().ContainSingle(finding => finding.Code == "SV3003");
    }

    [Fact]
    public void A_role_scoped_permission_with_no_tier_is_accepted()
    {
        var findings = Authorization(
            permissions:
            [
                new PermissionDefinition("probe:outbox:manage", "Probe", "Replay dead letters.")
                {
                    MinimumTier = null,
                    Roles = new HashSet<string>(StringComparer.Ordinal) { nameof(UserRole.Admin) }
                }
            ],
            policies: []);

        findings.Should().BeEmpty();
    }

    /// <summary>
    /// <b>SV3006, the check nothing else on the platform makes.</b> An unregistered policy does not
    /// throw when an endpoint names it — ASP.NET Core denies the request. Every endpoint behind it
    /// would answer 403 to a correctly authorised caller, and nothing would be logged.
    /// </summary>
    [Fact]
    public void A_policy_that_was_never_registered_with_asp_net_fails_the_boot()
    {
        var findings = Authorization(
            permissions: [new PermissionDefinition("probe:things:read", "Probe", "Read.")],
            policies:
            [
                PolicyDefinition.ForPermission("probe.things.read", "Probe", "probe:things:read")
            ],
            registerPolicies: false);

        findings.Should().ContainSingle(finding => finding.Code == "SV3006")
            .Which.Severity.Should().Be(StartupFindingSeverity.Error);
    }

    [Fact]
    public void A_registered_policy_is_accepted()
    {
        var findings = Authorization(
            permissions: [new PermissionDefinition("probe:things:read", "Probe", "Read.")],
            policies:
            [
                PolicyDefinition.ForPermission("probe.things.read", "Probe", "probe:things:read")
            ],
            registerPolicies: true);

        findings.Should().BeEmpty();
    }

    /// <summary>
    /// <b>An alias is validated exactly as hard as the name it stands in for.</b> A legacy name that
    /// was declared and never registered is the same silent 403 as an unregistered policy, and worse
    /// in one respect: the rename it exists to cover has already shipped, so the caller it was
    /// protecting is the one that breaks.
    /// </summary>
    [Fact]
    public void A_policy_alias_that_was_never_registered_fails_the_boot()
    {
        var findings = Authorization(
            permissions: [new PermissionDefinition("probe:things:read", "Probe", "Read.")],
            policies:
            [
                PolicyDefinition.ForPermission("probe.things.read", "Probe", "probe:things:read")
                    .WithAliases("Probe.Things.Read")
            ],
            registerPolicies: false);

        findings.Should().Contain(finding =>
            finding.Code == "SV3006" &&
            finding.Subject == "Probe.Things.Read" &&
            finding.Severity == StartupFindingSeverity.Error);
    }

    [Fact]
    public void A_registered_policy_alias_is_accepted()
    {
        var findings = Authorization(
            permissions: [new PermissionDefinition("probe:things:read", "Probe", "Read.")],
            policies:
            [
                PolicyDefinition.ForPermission("probe.things.read", "Probe", "probe:things:read")
                    .WithAliases("Probe.Things.Read")
            ],
            registerPolicies: true);

        findings.Should().BeEmpty();
    }

    // ── Module metadata ──────────────────────────────────────────────────

    [Fact]
    public void A_module_with_no_version_fails_the_boot()
    {
        var registry = new ModuleRegistry();
        registry.DiscoverModules([typeof(VersionlessModule).Assembly]);

        var findings = new ModuleMetadataValidator(registry)
            .Validate(Context())
            .ToList();

        findings.Should().Contain(finding => finding.Code == "SV1002" && finding.Subject == "Probe");
    }

    // ── Harnesses ────────────────────────────────────────────────────────

    private static List<StartupFinding> Features(
        PermissionDefinition permissions, FeatureDefinition features)
    {
        var permissionCatalog = new PermissionCatalog(
            [new TestPermissionContributor("Probe", permissions)]);

        var featureCatalog = new FeatureCatalog(
            [new TestFeatureContributor("Probe", features)]);

        return [.. new FeatureGateValidator(featureCatalog, permissionCatalog, Registry())
            .Validate(Context())];
    }

    private static List<StartupFinding> Authorization(
        PermissionDefinition[] permissions,
        PolicyDefinition[] policies,
        bool registerPolicies = true)
    {
        var permissionCatalog = new PermissionCatalog(
            [new TestPermissionContributor("Probe", permissions)]);

        var policyCatalog = new PolicyCatalog(
            [new TestPolicyContributor("Probe", policies)], permissionCatalog);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPolicyCatalog>(policyCatalog);
        services.AddAuthorization();

        if (registerPolicies)
        {
            services.AddPlatformAuthorizationPolicies();
        }

        using var provider = services.BuildServiceProvider();

        var validator = new AuthorizationCatalogValidator(
            permissionCatalog,
            policyCatalog,
            provider.GetRequiredService<IAuthorizationPolicyProvider>());

        return [.. validator.Validate(Context())];
    }

    /// <summary>
    /// A context with nothing in it. The three validators exercised here read their catalogues from
    /// constructor dependencies and never touch the registration snapshot, which is what makes them
    /// unit-testable at all — the two that do read it are covered by the integration suite against
    /// a real container.
    /// </summary>
    private static StartupValidationContext Context() =>
        new(new ServiceCollection().BuildServiceProvider(), [], [], new PlatformValidationOptions());

    private static ModuleRegistry Registry()
    {
        var registry = new ModuleRegistry();
        registry.DiscoverModules([typeof(VersionlessModule).Assembly]);
        return registry;
    }

    /// <summary>
    /// A module with a name and no version. Discovered by the registry the same way a real one is,
    /// because the validator reads what the registry composed rather than what a test handed it.
    /// </summary>
    private sealed class VersionlessModule : IPlatformModule
    {
        public string Name => "Probe";

        public string Version => string.Empty;

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
        }
    }
}
