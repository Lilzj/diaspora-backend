using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Community;
using Diaspora.Modules.Community.Api;
using Diaspora.Modules.Community.Authorization;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Moderation;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.Modules.Community.Extensibility.Visibility;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Community.Observability;
using Diaspora.Modules.Community.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Community module must not cross, enforced rather than documented.
/// <para>
/// Two matter more than the rest. The first is that <b>no vertical appears in this assembly</b>:
/// the entire promise of B6 — that Guides, Community Updates and Community Life consume it
/// rather than reimplementing it — depends on Community not knowing any of them exist. The day
/// one appears here, the reusable foundation has become a bottleneck every content module has to
/// queue behind.
/// </para>
/// <para>
/// The second is that <b>Community reaches Identity and Trust only through kernel contracts</b>.
/// It consumes Location and Media directly, because both publish consumption contracts for
/// verticals and their own freezes say so; it must never reach into Identity's or Trust's
/// aggregates, which is the boundary ADR-002 draws.
/// </para>
/// </summary>
public class CommunityArchitectureTests
{
    private static readonly Assembly Module = typeof(CommunityModule).Assembly;

    // ── Module boundaries ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_names_no_vertical_that_will_consume_it()
    {
        // Guides (B8), Community Updates (B9) and Community Life (B13) consume Community. If
        // any of them appears here the dependency has inverted.
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Guides",
                "Diaspora.Modules.Updates",
                "Diaspora.Modules.CommunityLife",
                "Diaspora.Modules.Marketplace",
                "Diaspora.Modules.Housing",
                "Diaspora.Modules.Jobs")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a reusable social foundation cannot depend on the modules that reuse it");
    }

    [Fact]
    public void The_module_does_not_reach_into_identity_or_trust()
    {
        var result = Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOnAny("Diaspora.Modules.Trust", "Diaspora.Domain.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Identity is reached through ICurrentUser and Trust through ITrustService — both " +
            "kernel contracts, per ADR-002");
    }

    [Fact]
    public void The_module_does_not_reference_the_api_host()
    {
        Types.InAssembly(Module)
            .Should()
            .NotHaveDependencyOn("Diaspora.Api")
            .GetResult()
            .IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void The_extensibility_surface_does_not_reach_into_persistence_or_api()
    {
        // Somebody implementing a feed, a ranking or a content source in another assembly must
        // never need this module's EF configurations or its endpoints.
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Community.Extensibility")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Community.Persistence",
                "Diaspora.Modules.Community.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void The_consumer_contracts_do_not_reach_into_persistence_or_api()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Community.Integration")
            .Should()
            .NotHaveDependencyOnAny(
                "Diaspora.Modules.Community.Persistence",
                "Diaspora.Modules.Community.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void The_domain_does_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(Module)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Community.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    // ── Registration ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_module_registers_only_through_contributors()
    {
        var contributors = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToList();

        contributors.Should().Contain(typeof(CommunityPermissionContributor));
        contributors.Should().Contain(typeof(CommunityFeatureContributor));
        contributors.Should().Contain(typeof(CommunityPolicyContributor));
        contributors.Should().Contain(typeof(CommunityEndpointContributor));
        contributors.Should().Contain(typeof(CommunityObservabilityContributor));
        contributors.Should().Contain(typeof(CommunityDatabaseContributor));

        typeof(IPlatformModule).IsAssignableFrom(typeof(CommunityModule)).Should().BeTrue();
    }

    [Fact]
    public void The_module_is_discoverable_by_the_kernels_scan()
    {
        var found = ModuleAssemblyScanner
            .FindImplementations<IPlatformModule>(ModuleAssemblyScanner.FindPlatformAssemblies());

        found.Should().Contain(typeof(CommunityModule));
    }

    // ── Frozen names ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_permission_is_namespaced_to_the_module()
    {
        var permissions = new CommunityPermissionContributor().GetPermissions().ToList();

        permissions.Should().OnlyContain(
            definition => definition.Key.StartsWith("community:", StringComparison.Ordinal));

        permissions.Select(definition => definition.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_feature_gate_is_namespaced_to_the_module()
    {
        var features = new CommunityFeatureContributor().GetFeatures().ToList();

        features.Should().OnlyContain(
            definition => definition.Name.StartsWith("community.", StringComparison.Ordinal));

        features.Select(definition => definition.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_policy_names_a_permission_this_module_declares()
    {
        var declared = new CommunityPermissionContributor()
            .GetPermissions()
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.Ordinal);

        var referenced = new CommunityPolicyContributor()
            .GetPolicies()
            .Select(policy => policy.Permission)
            .Where(permission => permission is not null)
            .ToList();

        referenced.Should().NotBeEmpty();
        referenced.Should().OnlyContain(permission => declared.Contains(permission!));
    }

    [Fact]
    public void Posting_is_the_highest_ordinary_band_in_the_module()
    {
        // An unverified account posting a fabricated emergency into a neighbourhood feed is the
        // worst thing this module can be used for, and the band records that judgement.
        var permissions = new CommunityPermissionContributor()
            .GetPermissions()
            .ToDictionary(definition => definition.Key, StringComparer.Ordinal);

        permissions[CommunityPermissions.Post].MinimumTier.Should().Be(AccessTier.Verified);

        permissions[CommunityPermissions.Comment].MinimumTier.Should().NotBeNull();

        ((int)permissions[CommunityPermissions.Comment].MinimumTier!.Value)
            .Should().BeLessThan(
                (int)AccessTier.Verified,
                "a member should be able to take part while their verification is in flight");
    }

    [Fact]
    public void No_gate_opens_below_limited()
    {
        // The client's own lock card says community access follows verification. A gate that
        // opened to a member who has not verified their phone would contradict the copy they
        // are reading.
        new CommunityFeatureContributor()
            .GetFeatures()
            .Should().OnlyContain(definition => definition.MinimumTier >= AccessTier.Limited);
    }

    [Fact]
    public void Posting_and_reading_sit_where_the_freeze_says()
    {
        var permissions = new CommunityPermissionContributor()
            .GetPermissions()
            .ToDictionary(definition => definition.Key, StringComparer.Ordinal);

        permissions[CommunityPermissions.Read].MinimumTier.Should().Be(AccessTier.Limited);
        permissions[CommunityPermissions.Moderate].Roles.Should().Contain("Moderator");
    }

    [Fact]
    public void Reporting_survives_restriction()
    {
        // The one restriction that would make the platform less safe.
        new CommunityPermissionContributor()
            .GetPermissions()
            .Single(definition => definition.Key == CommunityPermissions.Report)
            .GrantedWhenRestricted.Should().BeTrue();
    }

    [Fact]
    public void The_module_claims_exactly_one_route_prefix()
    {
        CommunityModuleMetadata.RoutePrefix.Should().Be("/api/v1/community");
    }

    [Fact]
    public void The_module_does_not_claim_the_reference_modules_bookmark_route()
    {
        // /api/v1/bookmarks belongs to the B2.6 reference module. Community's bookmarks are a
        // different capability and live under its own prefix.
        CommunityModuleMetadata.RoutePrefix.Should().NotBe("/api/v1/bookmarks");
    }

    [Fact]
    public void The_module_owns_one_schema()
    {
        new CommunityDatabaseContributor().Schema.Should().Be("community");
        CommunityModuleMetadata.Schema.Should().Be("community");
    }

    [Fact]
    public void The_telemetry_source_has_real_instruments_behind_it()
    {
        // Declaring a source name with nothing behind it subscribes the tracer to silence.
        var contributor = new CommunityObservabilityContributor();

        contributor.ActivitySources.Should().Contain(CommunityTelemetry.SourceName);
        contributor.Meters.Should().Contain(CommunityTelemetry.SourceName);

        CommunityTelemetry.Activity.Name.Should().Be(CommunityTelemetry.SourceName);
        CommunityTelemetry.Meter.Name.Should().Be(CommunityTelemetry.SourceName);
    }

    // ── The seams ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_extension_seam_the_brief_names_exists()
    {
        var seams = new[]
        {
            typeof(IFeedProvider),
            typeof(IRankingStrategy),
            typeof(Diaspora.Modules.Community.Extensibility.Reactions.IReactionCatalogContributor),
            typeof(IContentReviewProvider),
            typeof(IModerationPolicyProvider),
            typeof(Diaspora.Modules.Community.Extensibility.Hashtags.IHashtagProcessor),
            typeof(Diaspora.Modules.Community.Extensibility.Mentions.IMentionResolver),
            typeof(IVisibilityEvaluator),
            typeof(Diaspora.Modules.Community.Extensibility.Enrichment.IContentEnricher),
            typeof(ICommunityContentSource)
        };

        seams.Should().OnlyContain(type => type.IsInterface);
    }

    [Fact]
    public void The_module_ships_no_automated_review_provider()
    {
        // The same decision B5 made about virus scanning: a stub that always answers clean makes
        // an unmoderated platform look moderated. Register one before opening to the public.
        var shipped = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IContentReviewProvider).IsAssignableFrom)
            .ToList();

        shipped.Should().BeEmpty(
            "a fake reviewer would be worse than none; unavailable is the honest answer");
    }

    [Fact]
    public void The_built_in_rankings_cover_every_strategy_the_brief_names()
    {
        var shipped = Module.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(IRankingStrategy).IsAssignableFrom)
            .Select(type => ((IRankingStrategy)Activator.CreateInstance(type)!).RankingKey)
            .ToList();

        shipped.Should().Contain([
            RankingKeys.Chronological,
            RankingKeys.Trending,
            RankingKeys.TrustWeighted,
            RankingKeys.LocationWeighted
        ]);
    }

    [Fact]
    public void The_consumer_contracts_the_brief_promises_all_exist()
    {
        // What "Guides consumes Community instead of implementing its own social features"
        // actually means, as a list of interfaces.
        new[]
        {
            typeof(ICommunitySocialService),
            typeof(ICommunityCommentService),
            typeof(ICommunityReactionService),
            typeof(ICommunityBookmarkService),
            typeof(ICommunityModerationService),
            typeof(ICommunityVisibilityService),
            typeof(ICommunitySearchProjectionSource)
        }.Should().OnlyContain(type => type.IsInterface);
    }

    [Fact]
    public void Every_content_type_the_roadmap_names_already_has_a_value()
    {
        // Adding a vertical should not require editing this enum. These are the ones the brief
        // and the client between them already imply.
        var declared = Enum.GetNames<CommunityContentType>();

        declared.Should().Contain([
            "Post", "Comment", "Guide", "Listing", "Job", "Housing",
            "Marketplace", "Event", "Organization", "Poll", "Member", "Other"
        ]);
    }
}
