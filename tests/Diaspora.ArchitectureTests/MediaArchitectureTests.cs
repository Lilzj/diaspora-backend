using System.Reflection;
using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Media;
using Diaspora.Modules.Media.Api;
using Diaspora.Modules.Media.Authorization;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Infrastructure.Storage;
using Diaspora.Modules.Media.Options;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The boundaries the Media module must not cross, enforced rather than documented.
/// <para>
/// Two matter more than the rest. The first is that the domain names no storage SDK and no
/// image codec: the whole claim that S3, R2 or ImageSharp are configuration rather than code
/// rests on the domain never learning which one answered. The second is that no vertical
/// appears in this assembly — Media is consumable by Housing, Marketplace and the rest without
/// knowing any of them exist, and the day one appears here the hub has become a bottleneck.
/// </para>
/// </summary>
public sealed class MediaArchitectureTests
{
    private static Assembly MediaAssembly => typeof(MediaModule).Assembly;

    // ── Module boundaries ────────────────────────────────────────────────

    /// <summary>
    /// Trust is the module Media most obviously serves, and referencing it would be the most
    /// tempting shortcut. Trust reaches this module through the kernel's
    /// <see cref="IStorageService"/> — a published platform contract — and through nothing else.
    /// </summary>
    [Fact]
    public void Media_must_not_depend_on_the_trust_module()
    {
        var result = Types.InAssembly(MediaAssembly)
            .ShouldNot()
            .HaveDependencyOn("Diaspora.Modules.Trust")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Trust consumes Media through IStorageService, never directly: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Diaspora.Modules.Location")]
    [InlineData("Diaspora.Modules.Bookmarks")]
    [InlineData("Diaspora.Domain.Identity")]
    [InlineData("Diaspora.Application.Identity")]
    [InlineData("Diaspora.Api")]
    [InlineData("Diaspora.Infrastructure")]
    public void Media_must_not_depend_on_another_module_or_the_host(string forbidden)
    {
        var result = Types.InAssembly(MediaAssembly)
            .ShouldNot()
            .HaveDependencyOn(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The design rests on Media knowing no vertical. If Housing ever appears here, the access
    /// seam has been bypassed.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Marketplace")]
    [InlineData("Diaspora.Modules.Housing")]
    [InlineData("Diaspora.Modules.Jobs")]
    [InlineData("Diaspora.Modules.Community")]
    [InlineData("Diaspora.Modules.Providers")]
    [InlineData("Diaspora.Modules.Guides")]
    [InlineData("Diaspora.Modules.Messaging")]
    public void Media_must_not_depend_on_any_vertical(string vertical)
    {
        var result = Types.InAssembly(MediaAssembly)
            .ShouldNot()
            .HaveDependencyOn(vertical)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "verticals contribute to Media through IMediaAccessPolicy, never the other way " +
            "round: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── The storage seam ─────────────────────────────────────────────────

    /// <summary>
    /// The brief requires S3, Azure Blob, R2, GCS and MinIO to plug in without changing the
    /// domain. That is only true if nothing in the module names one — and the assertion covers
    /// the whole assembly, not just the domain, because a storage SDK anywhere in here would
    /// make the module undeployable without it.
    /// </summary>
    [Theory]
    [InlineData("Amazon")]
    [InlineData("AWSSDK")]
    [InlineData("Azure.Storage")]
    [InlineData("Google.Cloud")]
    [InlineData("Minio")]
    public void No_storage_sdk_reaches_this_module(string sdk)
    {
        var result = Types.InAssembly(MediaAssembly)
            .ShouldNot()
            .HaveDependencyOn(sdk)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the provider seam exists so the module never learns which store answered: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Every image codec is a licensing or native-dependency decision this module refuses to
    /// make on a deployment's behalf. The processor seam is how one arrives.
    /// </summary>
    [Theory]
    [InlineData("SixLabors")]
    [InlineData("SkiaSharp")]
    [InlineData("System.Drawing")]
    [InlineData("Magick")]
    [InlineData("FFMpeg")]
    public void No_image_or_video_codec_reaches_this_module(string codec)
    {
        var result = Types.InAssembly(MediaAssembly)
            .ShouldNot()
            .HaveDependencyOn(codec)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain must not reach EF Core either. Persistence configures the domain; the domain
    /// does not know it is persisted.
    /// </summary>
    [Fact]
    public void The_domain_must_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(MediaAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Media.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The domain must not know about HTTP either. A domain type that named <c>HttpContext</c>
    /// could not be reused by the background worker, which has no request.
    /// </summary>
    [Fact]
    public void The_domain_must_not_depend_on_aspnet_core()
    {
        var result = Types.InAssembly(MediaAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Media.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// A vertical implementing <see cref="IMediaAccessPolicy"/> or a deployment implementing
    /// <see cref="IMediaStorageProvider"/> must be able to do so without referencing anything
    /// of Media's persistence or API. Otherwise the seam has leaked.
    /// </summary>
    [Fact]
    public void The_extensibility_namespace_does_not_reach_into_persistence_or_api()
    {
        var result = Types.InAssembly(MediaAssembly)
            .That().ResideInNamespaceStartingWith("Diaspora.Modules.Media.Extensibility")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Diaspora.Modules.Media.Persistence",
                "Diaspora.Modules.Media.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// The default storage provider must not claim a capability it cannot deliver. Claiming
    /// presigned writes would make the pipeline plan an upload to a URL that does not exist.
    /// </summary>
    [Fact]
    public void The_default_storage_provider_declares_only_what_it_can_do()
    {
        var provider = new FilesystemStorageProvider(
            Microsoft.Extensions.Options.Options.Create(new MediaStorageOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FilesystemStorageProvider>.Instance);

        var capabilities = provider.Capabilities;

        capabilities.Should().HaveFlag(StorageCapability.RangeReads);
        capabilities.Should().NotHaveFlag(StorageCapability.PresignedRead);
        capabilities.Should().NotHaveFlag(StorageCapability.PresignedWrite);
        capabilities.Should().NotHaveFlag(StorageCapability.ServerSideCompose);
    }

    // ── Contributor registration ─────────────────────────────────────────

    [Fact]
    public void The_module_registers_through_every_contributor_the_platform_offers()
    {
        var types = MediaAssembly.GetTypes();

        types.Should().Contain(typeof(MediaPermissionContributor));
        types.Should().Contain(typeof(MediaFeatureContributor));
        types.Should().Contain(typeof(MediaPolicyContributor));
        types.Should().Contain(typeof(MediaEndpointContributor));
        types.Should().Contain(typeof(MediaObservabilityContributor));
        types.Should().Contain(typeof(MediaDatabaseContributor));

        typeof(IPlatformModule).IsAssignableFrom(typeof(MediaModule)).Should().BeTrue();
    }

    /// <summary>
    /// Declaring a telemetry source without creating an ActivitySource behind it subscribes
    /// the tracer to silence — the B2 mistake the extension guide names.
    /// </summary>
    [Fact]
    public void The_declared_telemetry_source_has_real_instruments_behind_it()
    {
        var contributor = new MediaObservabilityContributor();

        contributor.ActivitySources.Should().Contain(MediaTelemetry.SourceName);
        contributor.Meters.Should().Contain(MediaTelemetry.SourceName);

        MediaTelemetry.Activity.Name.Should().Be(MediaTelemetry.SourceName);
        MediaTelemetry.Meter.Name.Should().Be(MediaTelemetry.SourceName);
    }

    [Fact]
    public void Every_contributor_implementation_is_sealed()
    {
        var contributorTypes = MediaAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type =>
                typeof(IPermissionContributor).IsAssignableFrom(type) ||
                typeof(IFeatureContributor).IsAssignableFrom(type) ||
                typeof(IPolicyContributor).IsAssignableFrom(type) ||
                typeof(IEndpointContributor).IsAssignableFrom(type) ||
                typeof(IObservabilityContributor).IsAssignableFrom(type))
            .ToList();

        contributorTypes.Should().NotBeEmpty();
        contributorTypes.Should().OnlyContain(type => type.IsSealed);
    }

    // ── Frozen keys ──────────────────────────────────────────────────────

    [Fact]
    public void Every_declared_permission_is_owned_by_this_module()
    {
        var permissions = new MediaPermissionContributor().GetPermissions().ToList();

        permissions.Should().OnlyContain(
            permission => permission.Module == MediaModuleMetadata.Name);

        permissions.Should().OnlyContain(permission => permission.Key.StartsWith("media:"));
    }

    /// <summary>
    /// Adding a profile photograph is part of finishing onboarding, and the reputation model
    /// scores it as a trust signal. Gating it behind verification would make the signal
    /// unreachable for exactly the members who need it — a loop with no entrance.
    /// </summary>
    [Fact]
    public void Uploading_a_photograph_opens_at_the_onboarding_tier()
    {
        var permissions = new MediaPermissionContributor().GetPermissions().ToList();

        permissions.Single(p => p.Key == MediaPermissions.UploadImage)
            .MinimumTier.Should().Be(AccessTier.Onboarding);

        // Verification evidence is a document, and it is submitted before verification by
        // definition.
        permissions.Single(p => p.Key == MediaPermissions.UploadDocument)
            .MinimumTier.Should().Be(AccessTier.Onboarding);
    }

    /// <summary>Half a gigabyte of storage plus a transcode is a real cost.</summary>
    [Fact]
    public void Uploading_a_video_requires_verification()
    {
        new MediaPermissionContributor()
            .GetPermissions()
            .Single(p => p.Key == MediaPermissions.UploadVideo)
            .MinimumTier.Should().Be(AccessTier.Verified);
    }

    [Fact]
    public void Administration_is_restricted_to_staff_roles()
    {
        var permissions = new MediaPermissionContributor().GetPermissions().ToList();

        permissions.Single(p => p.Key == MediaPermissions.Administer)
            .Roles.Should().BeEquivalentTo("Moderator", "Admin", "SuperAdmin");

        permissions.Single(p => p.Key == MediaPermissions.ManageProcessing)
            .Roles.Should().BeEquivalentTo("Admin", "SuperAdmin");
    }

    /// <summary>
    /// The kernel validates this at startup too. Asserting it here means the failure surfaces
    /// in a unit run rather than as a boot failure in CI.
    /// </summary>
    [Fact]
    public void Every_policy_names_a_permission_this_module_declares()
    {
        var declared = new MediaPermissionContributor()
            .GetPermissions()
            .Select(permission => permission.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var policy in new MediaPolicyContributor().GetPolicies())
        {
            policy.Module.Should().Be(MediaModuleMetadata.Name);
            policy.Permission.Should().NotBeNull();
            declared.Should().Contain(policy.Permission!);
        }
    }

    [Fact]
    public void Every_feature_gate_is_owned_by_this_module()
    {
        new MediaFeatureContributor()
            .GetFeatures()
            .Should().OnlyContain(feature =>
                feature.Module == MediaModuleMetadata.Name &&
                feature.Name.StartsWith("media."));
    }

    [Fact]
    public void The_module_owns_exactly_one_database_schema()
    {
        var contributor = new MediaDatabaseContributor();

        contributor.Schema.Should().Be("media");
        contributor.Module.Should().Be(MediaModuleMetadata.Name);
    }

    [Fact]
    public void Every_route_hangs_off_the_modules_own_prefix()
    {
        MediaModuleMetadata.RoutePrefix.Should().Be("/api/v1/media");
    }

    [Fact]
    public void The_module_orders_after_identity_bookmarks_trust_and_location()
    {
        new MediaModule().Order.Should().BeGreaterThan(300);
    }

    // ── The extensibility contracts ──────────────────────────────────────

    /// <summary>
    /// The seams are the module's promise to every phase after this one. Renaming or removing
    /// one breaks a contract that deployments and verticals will already have implemented.
    /// </summary>
    [Fact]
    public void The_extension_seams_are_public_and_present()
    {
        typeof(IMediaStorageProvider).IsPublic.Should().BeTrue();
        typeof(IMediaProcessor).IsPublic.Should().BeTrue();
        typeof(IImageProcessor).IsPublic.Should().BeTrue();
        typeof(IVideoProcessor).IsPublic.Should().BeTrue();
        typeof(IMetadataExtractor).IsPublic.Should().BeTrue();
        typeof(IVirusScanner).IsPublic.Should().BeTrue();
        typeof(IThumbnailGenerator).IsPublic.Should().BeTrue();
        typeof(IOcrProvider).IsPublic.Should().BeTrue();
        typeof(IMediaTagger).IsPublic.Should().BeTrue();
        typeof(IMediaModerationProvider).IsPublic.Should().BeTrue();
        typeof(IMediaAccessPolicy).IsPublic.Should().BeTrue();
    }

    /// <summary>
    /// Every module named in the B5 brief as a future consumer already has an owner type, so a
    /// vertical can reference media on the day it ships without a Media release.
    /// </summary>
    [Theory]
    [InlineData(MediaOwnerType.Community)]
    [InlineData(MediaOwnerType.Provider)]
    [InlineData(MediaOwnerType.Guide)]
    [InlineData(MediaOwnerType.CommunityUpdate)]
    [InlineData(MediaOwnerType.Job)]
    [InlineData(MediaOwnerType.HousingListing)]
    [InlineData(MediaOwnerType.MarketplaceListing)]
    [InlineData(MediaOwnerType.Event)]
    [InlineData(MediaOwnerType.Organization)]
    [InlineData(MediaOwnerType.Message)]
    [InlineData(MediaOwnerType.Location)]
    [InlineData(MediaOwnerType.AiRequest)]
    public void Every_roadmap_consumer_already_has_an_owner_type(MediaOwnerType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }
}
