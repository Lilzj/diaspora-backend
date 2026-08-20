using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The controls that decide whether hostile bytes get stored.
/// <para>
/// The signature check is the cheapest and most valuable control in the module: the declared
/// content type comes from a header the caller wrote and the extension from a name the caller
/// chose, and only the magic bytes come from the file. These tests exist because that check
/// closes polyglot files, double extensions and "rename the payload to .jpg" in one
/// comparison — and because a regression in it would be silent.
/// </para>
/// </summary>
public sealed class MediaSecurityTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];

    private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();

    private static readonly byte[] Elf = [0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00];

    // ── Signature identification ─────────────────────────────────────────

    [Fact]
    public void The_signature_reader_identifies_the_formats_the_client_produces()
    {
        FileSignatureReader.Identify(Jpeg).ImageFormat.Should().Be(ImageFormat.Jpeg);
        FileSignatureReader.Identify(Png).ImageFormat.Should().Be(ImageFormat.Png);
        FileSignatureReader.Identify(Pdf).Kind.Should().Be(MediaKind.Document);
        FileSignatureReader.Identify(Zip).Kind.Should().Be(MediaKind.Archive);
    }

    /// <summary>
    /// RIFF and ISO base media containers share leading bytes across wildly different formats.
    /// Reading the brand rather than the first four bytes is what separates an iPhone
    /// photograph from an iPhone video — and therefore which size ceiling applies.
    /// </summary>
    [Fact]
    public void Container_formats_are_separated_by_their_brand_not_their_prefix()
    {
        var webp = "RIFF"u8.ToArray()
            .Concat(new byte[] { 0, 0, 0, 0 })
            .Concat("WEBP"u8.ToArray())
            .ToArray();

        var wav = "RIFF"u8.ToArray()
            .Concat(new byte[] { 0, 0, 0, 0 })
            .Concat("WAVE"u8.ToArray())
            .ToArray();

        var heic = new byte[] { 0, 0, 0, 0x18 }
            .Concat("ftypheic"u8.ToArray())
            .Concat(new byte[8])
            .ToArray();

        var mp4 = new byte[] { 0, 0, 0, 0x18 }
            .Concat("ftypisom"u8.ToArray())
            .Concat(new byte[8])
            .ToArray();

        FileSignatureReader.Identify(webp).ImageFormat.Should().Be(ImageFormat.WebP);
        FileSignatureReader.Identify(wav).Kind.Should().Be(MediaKind.Audio);
        FileSignatureReader.Identify(heic).ImageFormat.Should().Be(ImageFormat.Heic);
        FileSignatureReader.Identify(mp4).Kind.Should().Be(MediaKind.Video);
    }

    [Fact]
    public void Unknown_bytes_are_not_guessed_at()
    {
        FileSignatureReader.Identify(Elf).IsRecognized.Should().BeFalse();
        FileSignatureReader.Identify([1, 2]).IsRecognized.Should().BeFalse();
    }

    // ── The allow-list ───────────────────────────────────────────────────

    /// <summary>
    /// The attack this closes: a ZIP renamed to <c>.jpg</c>, declared as <c>image/jpeg</c>,
    /// and served from the platform's own origin.
    /// </summary>
    [Fact]
    public void An_archive_disguised_as_an_image_is_refused()
    {
        var signature = FileSignatureReader.Identify(Zip);
        var extension = FileExtension.TryCreate("jpg").Value;

        MediaTypeAllowList.AllowsSignature(MediaKind.Image, signature, extension)
            .Should().BeFalse();
    }

    /// <summary>
    /// A <c>.docx</c> genuinely is a ZIP archive. Refusing it would refuse every Word document
    /// on the platform; accepting a ZIP as a document requires an Office extension as well.
    /// </summary>
    [Fact]
    public void A_zip_backed_office_document_is_accepted_only_with_an_office_extension()
    {
        var signature = FileSignatureReader.Identify(Zip);

        MediaTypeAllowList.AllowsSignature(
            MediaKind.Document, signature, FileExtension.TryCreate("docx").Value)
            .Should().BeTrue();

        MediaTypeAllowList.AllowsSignature(
            MediaKind.Document, signature, FileExtension.TryCreate("pdf").Value)
            .Should().BeFalse();
    }

    /// <summary>
    /// An SVG is XML that can carry script. Serving one from the platform's own origin is
    /// stored cross-site scripting, and sanitising one correctly is a research problem.
    /// </summary>
    [Fact]
    public void Vector_images_are_not_accepted_at_all()
    {
        MediaTypeAllowList.AllowsContentType(
            MediaKind.Image, MediaContentType.Create("image/svg+xml"))
            .Should().BeFalse();

        MediaTypeAllowList.AllowsExtension(
            MediaKind.Image, FileExtension.TryCreate("svg").Value)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("application/x-sh")]
    [InlineData("text/html")]
    public void Executable_and_markup_types_are_not_accepted(string contentType)
    {
        var type = MediaContentType.Create(contentType);

        MediaTypeAllowList.AllowsContentType(MediaKind.Image, type).Should().BeFalse();
        MediaTypeAllowList.AllowsContentType(MediaKind.Document, type).Should().BeFalse();
    }

    /// <summary>
    /// <c>expo-image-picker</c> on iOS returns HEIC unless the caller opts out, and no screen
    /// in the app opts out. Rejecting it rejects the default iPhone camera output.
    /// </summary>
    [Fact]
    public void Heic_is_accepted_because_it_is_the_ios_camera_default()
    {
        MediaTypeAllowList.AllowsContentType(
            MediaKind.Image, MediaContentType.Create("image/heic"))
            .Should().BeTrue();
    }

    /// <summary>
    /// The narrowest list on the platform, and it is the client's own published one:
    /// "Supports PDF, PNG, JPG".
    /// </summary>
    [Fact]
    public void Verification_evidence_accepts_only_what_the_client_advertises()
    {
        foreach (var accepted in new[] { "application/pdf", "image/png", "image/jpeg" })
        {
            MediaTypeAllowList.AllowsContentType(
                MediaKind.VerificationDocument, MediaContentType.Create(accepted))
                .Should().BeTrue();
        }

        MediaTypeAllowList.AllowsContentType(
            MediaKind.VerificationDocument, MediaContentType.Create("video/mp4"))
            .Should().BeFalse();

        MediaTypeAllowList.AllowsContentType(
            MediaKind.VerificationDocument, MediaContentType.Create("application/zip"))
            .Should().BeFalse();
    }

    // ── Limits ───────────────────────────────────────────────────────────

    /// <summary>
    /// Frozen against the frontend contract report. The client tells members "Max 5MB" on two
    /// screens and Trust's own domain constant agrees; a backend that quietly enforced a
    /// different number would be contradicting its own UI.
    /// </summary>
    [Fact]
    public void The_verification_ceiling_matches_what_the_client_advertises()
    {
        MediaLimits.MaxSizeFor(MediaKind.VerificationDocument)
            .Should().Be(ByteSize.FromMegabytes(5));
    }

    /// <summary>
    /// The reason Trust was willing to hand its document storage to another module. Not a
    /// default a caller can override — a hard refusal in the domain.
    /// </summary>
    [Fact]
    public void A_verification_document_can_never_be_made_public()
    {
        MediaLimits.CanBePublic(MediaKind.VerificationDocument).Should().BeFalse();
        MediaLimits.CanBePublic(MediaKind.Image).Should().BeTrue();

        MediaLimits.DefaultVisibilityFor(MediaKind.Image)
            .Should().Be(MediaVisibility.Private);
    }

    /// <summary>Frozen against the frontend contract report's Upload Flow Inventory.</summary>
    [Fact]
    public void Collection_capacities_match_the_client_screens_they_serve()
    {
        // post.tsx: "Add up to 4 high-quality photos", four rendered slots.
        MediaLimits.MaxItemsFor(MediaCollectionKind.ProviderGallery).Should().Be(4);

        // AttachmentPicker.tsx: selectionLimit: 5.
        MediaLimits.MaxItemsFor(MediaCollectionKind.MessageAttachments).Should().Be(5);
    }

    /// <summary>
    /// A verification document is reviewed at full size by a human and never appears in a
    /// gallery. Generating a ladder for one would be storage spent on renditions nothing
    /// requests.
    /// </summary>
    [Fact]
    public void No_size_ladder_is_planned_for_a_verification_document()
    {
        MediaLimits.LadderFor(MediaKind.VerificationDocument).Should().BeEmpty();
        MediaLimits.LadderFor(MediaKind.Image).Should().HaveCount(5);
        MediaLimits.LadderFor(MediaKind.Document).Should().BeEmpty();
    }

    /// <summary>
    /// Deliberately shorter than the general lifetime and matched to Trust's own
    /// <c>DocumentLinkFactory.LinkLifetime</c>: an immigration-document link that outlives the
    /// page it was rendered on is one that ends up pasted into a support ticket.
    /// </summary>
    [Fact]
    public void A_sensitive_link_lives_no_longer_than_trust_allows()
    {
        MediaLimits.SensitiveSignedUrlLifetime.Should().Be(TimeSpan.FromMinutes(10));
        MediaLimits.SensitiveSignedUrlLifetime.Should()
            .BeLessThan(MediaLimits.SignedUrlLifetime);
    }
}
