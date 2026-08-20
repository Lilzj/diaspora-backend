using Diaspora.Modules.Media;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Infrastructure.Processing;
using Diaspora.Modules.Media.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The seams every later phase binds to.
/// <para>
/// These tests are about the module's promise to code that does not exist yet: a deployment's
/// S3 provider, a vertical's access policy, a codec somebody installs in 2027. The behaviour
/// they pin down is what those authors will rely on — a missing capability degrading rather
/// than failing, a collision failing loudly rather than resolving by load order, an abstention
/// leaving the default decision intact.
/// </para>
/// </summary>
public sealed class MediaExtensibilityTests
{
    // ── The processor registry ───────────────────────────────────────────

    /// <summary>
    /// The built-in set: a header metadata extractor and a JPEG metadata stripper, both pure
    /// managed code. No codec, no transcoder, no scanner — each of those is a licensing or
    /// infrastructure decision a module must not make on a deployment's behalf.
    /// </summary>
    [Fact]
    public void The_module_ships_metadata_capabilities_and_no_codec()
    {
        var registry = BuiltInRegistry();

        registry.Supports(MediaKind.Image, ProcessingOperation.ExtractMetadata)
            .Should().BeTrue();

        registry.Supports(MediaKind.Image, ProcessingOperation.StripMetadata)
            .Should().BeTrue();

        registry.Supports(MediaKind.Image, ProcessingOperation.GenerateThumbnails)
            .Should().BeFalse();

        registry.Supports(MediaKind.Video, ProcessingOperation.TranscodeVideo)
            .Should().BeFalse();
    }

    /// <summary>
    /// Registering a fake scanner that always says "clean" would be worse than shipping none:
    /// it makes an unscanned platform look like a scanned one.
    /// </summary>
    [Fact]
    public void No_malware_scanner_ships_and_the_module_says_so()
    {
        BuiltInRegistry().HasScanner.Should().BeFalse();
    }

    /// <summary>
    /// A deployment installs a better processor by registering it at a lower order. The
    /// built-in stays behind it as a fallback.
    /// </summary>
    [Fact]
    public void A_lower_ordered_processor_takes_precedence()
    {
        var registry = new MediaProcessorRegistry(
        [
            new HeaderMetadataExtractor(),
            new PreferredExtractor()
        ]);

        registry.For(MediaKind.Image, ProcessingOperation.ExtractMetadata)[0]
            .Name.Should().Be("preferred");
    }

    /// <summary>
    /// Several processors claiming one operation is a preference list, not an error — unlike
    /// storage providers and access policies, where a collision means an unanswerable question.
    /// </summary>
    [Fact]
    public void Two_processors_claiming_one_operation_is_allowed()
    {
        var registry = new MediaProcessorRegistry(
        [
            new HeaderMetadataExtractor(),
            new PreferredExtractor()
        ]);

        registry.For(MediaKind.Image, ProcessingOperation.ExtractMetadata)
            .Should().HaveCount(2);
    }

    // ── Storage provider registry ────────────────────────────────────────

    /// <summary>
    /// A platform where which provider you got depended on assembly load order would be one
    /// where a deployment reads from one bucket and writes to another without noticing.
    /// </summary>
    [Fact]
    public void Two_providers_claiming_one_name_fails_loudly()
    {
        var act = () => new StorageProviderRegistry(
            [new FakeProvider("s3"), new FakeProvider("s3")],
            Options.Create(new MediaStorageOptions()));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*claim the name*");
    }

    /// <summary>
    /// The property that makes a provider migration gradual and reversible: an object written
    /// to MinIO in 2026 still resolves through MinIO in 2028, whatever the default has become.
    /// </summary>
    [Fact]
    public void A_provider_is_resolved_by_the_name_the_object_recorded()
    {
        var registry = new StorageProviderRegistry(
            [new FakeProvider("minio"), new FakeProvider("s3")],
            Options.Create(new MediaStorageOptions { DefaultProvider = "s3" }));

        registry.Default.Name.Should().Be("s3");
        registry.Resolve("minio").Value.Name.Should().Be("minio");
    }

    /// <summary>
    /// A recoverable configuration mistake — somebody removed a provider a deployment still
    /// has objects in — surfaces as a service failure rather than a crash.
    /// </summary>
    [Fact]
    public void An_unregistered_provider_is_a_failure_not_an_exception()
    {
        var registry = new StorageProviderRegistry(
            [new FakeProvider("filesystem")],
            Options.Create(new MediaStorageOptions()));

        var resolved = registry.Resolve("azure-blob");

        resolved.IsFailure.Should().BeTrue();
        resolved.Error.Code.Should().Be(MediaErrorCodes.StorageProviderUnknown);
    }

    // ── Access policies ──────────────────────────────────────────────────

    /// <summary>
    /// An access decision that depended on assembly load order would be an access decision
    /// nobody could reproduce, and the failure would be silent.
    /// </summary>
    [Fact]
    public void Two_policies_claiming_one_owner_type_fails_loudly()
    {
        var act = () => new MediaAccessPolicyRegistry(
        [
            new FakePolicy(MediaOwnerType.HousingListing, MediaAccessDecision.Allow()),
            new FakePolicy(MediaOwnerType.HousingListing, MediaAccessDecision.Deny())
        ]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*claim owner type*");
    }

    /// <summary>
    /// The normal case for a vertical that has not shipped yet. It must not be a denial: an
    /// asset owned by a type nobody claims still belongs to its uploader.
    /// </summary>
    [Fact]
    public async Task An_unclaimed_owner_type_abstains_rather_than_denying()
    {
        var registry = new MediaAccessPolicyRegistry([]);

        var decision = await registry.EvaluateAsync(Request(MediaOwnerType.HousingListing));

        decision.IsAbstention.Should().BeTrue();
        decision.IsDenial.Should().BeFalse();
    }

    [Fact]
    public async Task A_vertical_can_deny_access_to_its_own_media()
    {
        var registry = new MediaAccessPolicyRegistry(
        [
            new FakePolicy(MediaOwnerType.HousingListing, MediaAccessDecision.Deny("unpublished"))
        ]);

        var decision = await registry.EvaluateAsync(Request(MediaOwnerType.HousingListing));

        decision.IsDenial.Should().BeTrue();
    }

    /// <summary>
    /// Without abstention, a policy that only cares about floor plans would have to return
    /// <c>Allow</c> for every gallery photograph — which reads as a deliberate grant and is
    /// indistinguishable from one in an audit.
    /// </summary>
    [Fact]
    public async Task A_policy_that_has_no_opinion_leaves_the_default_standing()
    {
        var registry = new MediaAccessPolicyRegistry(
        [
            new FakePolicy(MediaOwnerType.HousingListing, MediaAccessDecision.Abstain())
        ]);

        var decision = await registry.EvaluateAsync(Request(MediaOwnerType.HousingListing));

        decision.IsDenial.Should().BeFalse();
        decision.IsAbstention.Should().BeTrue();
    }

    // ── Pipeline planning ────────────────────────────────────────────────

    /// <summary>
    /// Planned from the kind rather than from what is installed. A plan that omitted the steps
    /// nothing can perform would report complete success while producing no thumbnails at all,
    /// and an operator would have nothing to look at.
    /// </summary>
    [Fact]
    public void The_plan_is_the_same_whatever_a_deployment_has_installed()
    {
        var plan = Modules.Media.Application.Services.MediaProcessingPipeline
            .Plan(MediaKind.Image);

        plan.Should().Contain(ProcessingOperation.ValidateSignature);
        plan.Should().Contain(ProcessingOperation.ScanForMalware);
        plan.Should().Contain(ProcessingOperation.ExtractMetadata);
        plan.Should().Contain(ProcessingOperation.StripMetadata);
        plan.Should().Contain(ProcessingOperation.GenerateThumbnails);
    }

    /// <summary>
    /// Signature validation first, because everything after assumes the file is what it claims.
    /// Scanning second, because nothing should decode bytes that have not been scanned — a
    /// decoder is the largest attack surface in the pipeline.
    /// </summary>
    [Fact]
    public void Validation_precedes_scanning_and_scanning_precedes_decoding()
    {
        var plan = Modules.Media.Application.Services.MediaProcessingPipeline
            .Plan(MediaKind.Image)
            .ToList();

        plan.IndexOf(ProcessingOperation.ValidateSignature)
            .Should().BeLessThan(plan.IndexOf(ProcessingOperation.ScanForMalware));

        plan.IndexOf(ProcessingOperation.ScanForMalware)
            .Should().BeLessThan(plan.IndexOf(ProcessingOperation.GenerateThumbnails));

        plan.IndexOf(ProcessingOperation.ExtractMetadata)
            .Should().BeLessThan(plan.IndexOf(ProcessingOperation.GenerateThumbnails));
    }

    /// <summary>
    /// A verification document is reviewed at full size by a human. Generating a ladder for one
    /// would be storage spent on renditions nothing requests.
    /// </summary>
    [Fact]
    public void No_renditions_are_planned_for_a_verification_document()
    {
        var plan = Modules.Media.Application.Services.MediaProcessingPipeline
            .Plan(MediaKind.VerificationDocument);

        plan.Should().NotContain(ProcessingOperation.GenerateThumbnails);

        // But its metadata is still stripped: a photographed passport carries GPS too.
        plan.Should().Contain(ProcessingOperation.StripMetadata);
        plan.Should().Contain(ProcessingOperation.ScanForMalware);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static MediaProcessorRegistry BuiltInRegistry() =>
        new([new HeaderMetadataExtractor(), new JpegMetadataStripper()]);

    private static MediaAccessRequest Request(MediaOwnerType type) => new()
    {
        MediaId = Diaspora.SharedKernel.Identity.MediaId.New(),
        OwnerType = type,
        OwnerId = Guid.NewGuid(),
        RequestedLevel = MediaAccessLevel.Read,
        UploadedBy = Diaspora.SharedKernel.Identity.UserId.New()
    };
}
