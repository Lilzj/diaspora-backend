using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The Phase 0 frontend contract, as executable assertions.
/// <para>
/// <b>Every value here is quoted from the frontend contract report, not chosen.</b> The report
/// is a document, and a document cannot fail a build — so the numbers and names it froze live
/// here as well, where changing one breaks a test and forces the change to be deliberate.
/// </para>
/// <para>
/// The single most valuable group is the upload-state vocabulary. The client's
/// <c>AttachmentUploadState</c> has five values, its reducer in <c>store/draft.ts</c> switches
/// on all five, and a backend that renamed one would silently break a progress bar in a way
/// no backend test would catch.
/// </para>
/// </summary>
public sealed class MediaFrontendContractTests
{
    // ── Upload state vocabulary ──────────────────────────────────────────

    /// <summary>
    /// <c>'queued' | 'uploading' | 'paused' | 'completed' | 'failed'</c>, from
    /// <c>src/types/conversation.ts</c>. Carried across letter-for-letter.
    /// </summary>
    [Theory]
    [InlineData(UploadSessionStatus.Queued, "queued")]
    [InlineData(UploadSessionStatus.Uploading, "uploading")]
    [InlineData(UploadSessionStatus.Paused, "paused")]
    [InlineData(UploadSessionStatus.Completed, "completed")]
    [InlineData(UploadSessionStatus.Failed, "failed")]
    public void The_upload_states_are_the_clients_own(UploadSessionStatus status, string wire)
    {
        MediaWireNames.ToWire(status).Should().Be(wire);
    }

    /// <summary>
    /// <c>'paused'</c> is why uploads are sessions rather than a single POST — a one-shot
    /// upload can never reach a state the client's UI already renders.
    /// </summary>
    [Fact]
    public void The_paused_state_the_client_renders_is_reachable()
    {
        Enum.IsDefined(UploadSessionStatus.Paused).Should().BeTrue();
    }

    // ── Metadata field names ─────────────────────────────────────────────

    /// <summary>
    /// <c>width</c>, <c>height</c>, <c>durationSeconds</c> and <c>pageCount</c> are declared on
    /// the client's attachment DTOs and populated by nothing in the app. This response is what
    /// fills them, and it must keep their names or the client's renderers need a rewrite.
    /// </summary>
    [Fact]
    public void The_metadata_response_carries_the_clients_field_names()
    {
        var properties = typeof(MediaMetadataResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain("Width");
        properties.Should().Contain("Height");
        properties.Should().Contain("DurationSeconds");
        properties.Should().Contain("PageCount");
    }

    /// <summary>
    /// <c>sizeBytes</c>, <c>mimeType</c> as <c>contentType</c>, <c>fileName</c>,
    /// <c>thumbnailUrl</c>, <c>caption</c> — the shape <c>MessageBubble</c> already renders.
    /// </summary>
    [Fact]
    public void The_asset_response_carries_the_clients_attachment_fields()
    {
        var properties = typeof(MediaAssetResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain("SizeBytes");
        properties.Should().Contain("ContentType");
        properties.Should().Contain("FileName");
        properties.Should().Contain("ThumbnailUrl");
        properties.Should().Contain("Caption");
        properties.Should().Contain("Url");
    }

    /// <summary>The 0–100 number <c>post.tsx</c>'s per-slot progress bar already renders.</summary>
    [Fact]
    public void The_upload_response_reports_progress_as_a_percentage()
    {
        typeof(UploadSessionResponse)
            .GetProperty(nameof(UploadSessionResponse.ProgressPercent))!
            .PropertyType.Should().Be<int>();
    }

    /// <summary>Resumption is only possible if the client can see which parts arrived.</summary>
    [Fact]
    public void The_upload_response_reports_which_parts_arrived()
    {
        typeof(UploadSessionResponse)
            .GetProperty(nameof(UploadSessionResponse.ReceivedChunks))
            .Should().NotBeNull();
    }

    // ── Frozen limits ────────────────────────────────────────────────────

    /// <summary>
    /// "Supports PDF, PNG, JPG (Max 5MB)" — <c>verification/country.tsx:276</c> and
    /// <c>verification/diaspora.tsx:498</c>. Trust's own <c>VerificationDocument</c> agrees.
    /// A backend enforcing a different number would contradict its own UI.
    /// </summary>
    [Fact]
    public void The_verification_document_ceiling_is_the_five_megabytes_the_ui_promises()
    {
        MediaLimits.MaxSizeFor(MediaKind.VerificationDocument)
            .Should().Be(ByteSize.FromMegabytes(5));
    }

    /// <summary>"Add up to 4 high-quality photos" — <c>(tabs)/post.tsx</c>, four slots.</summary>
    [Fact]
    public void The_provider_gallery_holds_the_four_slots_the_wizard_renders()
    {
        MediaLimits.MaxItemsFor(MediaCollectionKind.ProviderGallery).Should().Be(4);
    }

    /// <summary><c>selectionLimit: 5</c> — <c>components/conversation/AttachmentPicker.tsx</c>.</summary>
    [Fact]
    public void Message_attachments_hold_the_five_the_picker_allows()
    {
        MediaLimits.MaxItemsFor(MediaCollectionKind.MessageAttachments).Should().Be(5);
    }

    /// <summary>
    /// Every picker call in the app passes <c>quality: 0.8</c>. Matching it means a re-encode
    /// does not visibly degrade what the client already compressed.
    /// </summary>
    [Fact]
    public void The_default_rendition_quality_matches_the_pickers()
    {
        ImageTransformation.DefaultQuality.Should().Be(80);
    }

    // ── The variant ladder ───────────────────────────────────────────────

    /// <summary>
    /// Every mock URL in the client carries <c>?w=600</c> or <c>?w=120</c>, and the avatar
    /// component renders at 50 px. The ladder was chosen from those, not invented.
    /// </summary>
    [Fact]
    public void The_size_ladder_matches_the_widths_the_client_asks_for()
    {
        ImageTransformation.ForPurpose(MediaVariantPurpose.Medium).Value
            .LongestEdge.Should().Be(600);

        ImageTransformation.ForPurpose(MediaVariantPurpose.Thumbnail).Value
            .LongestEdge.Should().Be(96);
    }

    /// <summary>An avatar is square; a listing photograph is not cropped to a card.</summary>
    [Fact]
    public void Thumbnails_crop_square_and_everything_else_fits_inside_its_box()
    {
        ImageTransformation.ForPurpose(MediaVariantPurpose.Thumbnail).Value
            .Mode.Should().Be(ResizeMode.Cover);

        ImageTransformation.ForPurpose(MediaVariantPurpose.Medium).Value
            .Mode.Should().Be(ResizeMode.Contain);
    }

    /// <summary>Every rendition strips embedded metadata. A phone photograph carries GPS.</summary>
    [Fact]
    public void Every_rendition_strips_embedded_metadata()
    {
        foreach (var purpose in MediaLimits.LadderFor(MediaKind.Image))
        {
            ImageTransformation.ForPurpose(purpose).Value
                .StripMetadata.Should().BeTrue();
        }
    }

    // ── Naming unification ───────────────────────────────────────────────

    /// <summary>
    /// The client has fourteen names for two concepts across thirty-one fields. These are the
    /// canonical ones, published so Housing and Marketplace do not invent <c>cover</c>,
    /// <c>coverImage</c> and <c>cover_photo</c> independently.
    /// </summary>
    [Fact]
    public void The_canonical_reference_roles_are_published()
    {
        MediaReferenceRoles.Avatar.Should().Be("avatar");
        MediaReferenceRoles.Cover.Should().Be("cover");
        MediaReferenceRoles.Logo.Should().Be("logo");
        MediaReferenceRoles.Gallery.Should().Be("gallery");
        MediaReferenceRoles.Document.Should().Be("document");
        MediaReferenceRoles.Selfie.Should().Be("selfie");
    }

    /// <summary>
    /// Intentional deviation D1: <c>documentUrl</c> and <c>documentUri</c> both held storage
    /// keys and neither held a URL. A business module holds an identifier and a role, and
    /// nothing else — no URL field exists anywhere on the reference contract.
    /// </summary>
    [Fact]
    public void A_media_reference_carries_no_url()
    {
        var properties = typeof(MediaReferenceResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain("MediaId");
        properties.Should().Contain("Role");
        properties.Should().NotContain(name => name.Contains("Url", StringComparison.Ordinal));
        properties.Should().NotContain(name => name.Contains("Key", StringComparison.Ordinal));
    }

    // ── Wire naming ──────────────────────────────────────────────────────

    /// <summary>
    /// An enum serialised as a number is a contract nobody can read and that breaks silently
    /// when a value is inserted in the middle.
    /// </summary>
    [Theory]
    [InlineData(MediaKind.VerificationDocument, "verificationDocument")]
    [InlineData(MediaKind.Image, "image")]
    [InlineData(MediaVariantPurpose.Thumbnail, "thumbnail")]
    [InlineData(MediaOwnerType.HousingListing, "housingListing")]
    [InlineData(VirusScanStatus.NotScanned, "notScanned")]
    public void Enums_travel_as_camel_cased_names(object value, string expected)
    {
        var wire = value switch
        {
            MediaKind kind => MediaWireNames.ToWire(kind),
            MediaVariantPurpose purpose => MediaWireNames.ToWire(purpose),
            MediaOwnerType owner => MediaWireNames.ToWire(owner),
            VirusScanStatus scan => MediaWireNames.ToWire(scan),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        wire.Should().Be(expected);
    }

    /// <summary>
    /// Clients differ in how they capitalise. Refusing the ones that disagree buys nothing but
    /// support tickets — but an unknown value must still be refused rather than defaulted,
    /// because a typo silently becoming <c>Unknown</c> is how an upload gets the wrong ceiling.
    /// </summary>
    [Fact]
    public void Parsing_accepts_any_casing_and_refuses_the_unknown()
    {
        MediaWireNames.Parse<MediaKind>(
            "VERIFICATIONDOCUMENT",
            Diaspora.SharedKernel.Results.Error.Validation("x", "y"))
            .Value.Should().Be(MediaKind.VerificationDocument);

        MediaWireNames.Parse<MediaKind>(
            "photograph",
            Diaspora.SharedKernel.Results.Error.Validation("x", "y"))
            .IsFailure.Should().BeTrue();
    }

    // ── Capabilities ─────────────────────────────────────────────────────

    /// <summary>
    /// Nothing in the client checks a file before accepting it, so a client cannot warn a
    /// member about a limit it does not know. This response is how it learns them.
    /// </summary>
    [Fact]
    public void The_capabilities_response_tells_a_client_every_limit_it_needs()
    {
        var properties = typeof(MediaCapabilitiesResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().Contain("MaxUploadBytes");
        properties.Should().Contain("DirectUploadCeilingBytes");
        properties.Should().Contain("ChunkSizeBytes");
        properties.Should().Contain("AcceptedContentTypes");
        properties.Should().Contain("MalwareScanningEnabled");
    }

    /// <summary>
    /// The published list and the enforced list read the same sets, so they cannot drift —
    /// which is exactly the failure the frontend already has, where the verification screen
    /// advertises "PDF, PNG, JPG" and nothing checks it.
    /// </summary>
    [Fact]
    public void The_published_type_list_agrees_with_the_enforced_one()
    {
        foreach (var kind in new[]
        {
            MediaKind.Image, MediaKind.Video, MediaKind.Audio,
            MediaKind.Document, MediaKind.Archive, MediaKind.VerificationDocument
        })
        {
            foreach (var published in MediaTypeCatalog.AcceptedContentTypesFor(kind))
            {
                MediaTypeAllowList.AllowsContentType(
                    kind, MediaContentType.Create(published))
                    .Should().BeTrue(
                        $"{published} is published as accepted for {kind} and must be accepted");
            }
        }
    }
}
