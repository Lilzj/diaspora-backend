using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The value objects that decide what a file is and what it may be called.
/// <para>
/// Weighted towards the security-relevant behaviour, because that is where a value object
/// failing quietly costs the most: a filename that keeps its traversal segments, an extension
/// read from the wrong dot, a content type that carries a header parameter into a stored field.
/// </para>
/// </summary>
public sealed class MediaValueObjectTests
{
    // ── Content type ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/jpeg", "image", "jpeg")]
    [InlineData("IMAGE/JPEG", "image", "jpeg")]
    [InlineData("  application/pdf  ", "application", "pdf")]
    public void A_content_type_is_normalised(string input, string type, string subtype)
    {
        var parsed = MediaContentType.TryCreate(input);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Type.Should().Be(type);
        parsed.Value.Subtype.Should().Be(subtype);
    }

    /// <summary>
    /// Parameters are transport detail. Letting one into the stored value means two rows for
    /// one format and a lookup that misses half of them.
    /// </summary>
    [Fact]
    public void A_content_type_drops_its_parameters()
    {
        MediaContentType.Create("text/plain; charset=utf-8").Value.Should().Be("text/plain");
    }

    [Theory]
    [InlineData("")]
    [InlineData("image")]
    [InlineData("image/")]
    [InlineData("image/jpeg/extra")]
    [InlineData("image/jpeg\r\nX-Injected: 1")]
    public void A_malformed_content_type_is_refused(string input)
    {
        MediaContentType.TryCreate(input).IsFailure.Should().BeTrue();
    }

    // ── File extension ───────────────────────────────────────────────────

    /// <summary>
    /// Only the final segment counts. The whole reason for parsing rather than searching the
    /// name for a known-good substring is that <c>photo.jpg.exe</c> is an executable.
    /// </summary>
    [Theory]
    [InlineData("photo.jpg", "jpg")]
    [InlineData("photo.JPG", "jpg")]
    [InlineData("photo.jpg.exe", "exe")]
    [InlineData("archive.tar.gz", "gz")]
    [InlineData("noextension", "")]
    [InlineData("trailing.", "")]
    public void An_extension_is_the_final_segment(string fileName, string expected)
    {
        FileExtension.FromFileName(fileName).Value.Should().Be(expected);
    }

    // ── Safe file name ───────────────────────────────────────────────────

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    [InlineData("/absolute/path.jpg")]
    public void A_file_name_cannot_carry_a_path(string hostile)
    {
        var safe = SafeFileName.Create(hostile);

        safe.Value.Should().NotContain("..");
        safe.Value.Should().NotContain("/");
        safe.Value.Should().NotContain("\\");
    }

    /// <summary>
    /// A right-to-left override makes <c>gpj.exe</c> render as <c>exe.jpg</c> in a file
    /// listing. It is a Unicode format character, not a letter, so the allow-list drops it.
    /// </summary>
    [Fact]
    public void A_file_name_drops_bidirectional_override_characters()
    {
        var safe = SafeFileName.Create("invoice‮gpj.exe");

        safe.Value.Should().NotContain("‮");
    }

    /// <summary>
    /// A member who uploaded a French filename should see it back. Sanitisation is for the
    /// storage key and the header, not for what the member reads.
    /// </summary>
    [Fact]
    public void A_file_name_keeps_its_original_for_display()
    {
        var safe = SafeFileName.Create("Passeport – Côte d'Ivoire.pdf");

        safe.Original.Should().Be("Passeport – Côte d'Ivoire.pdf");
        safe.Value.Should().Contain("C");
        safe.Extension.Value.Should().Be("pdf");
    }

    /// <summary>
    /// <c>CON</c> is not a creatable file on Windows, and a filesystem provider running there
    /// would fail on it in a way that looks like a storage outage.
    /// </summary>
    [Theory]
    [InlineData("CON")]
    [InlineData("lpt1")]
    [InlineData("NUL")]
    public void A_reserved_windows_device_name_is_replaced(string reserved)
    {
        SafeFileName.Create(reserved).Value.Should().Be(SafeFileName.Fallback);
    }

    /// <summary>Refusing an upload over its name is worse than renaming it.</summary>
    [Fact]
    public void A_file_name_that_survives_nothing_falls_back_rather_than_failing()
    {
        SafeFileName.Create("!!!@@@###").Value.Should().Be(SafeFileName.Fallback);
    }

    /// <summary>
    /// The sanitised form must be a fixed point of sanitisation, because persistence stores the
    /// original and derives the sanitised form on every read.
    /// </summary>
    [Fact]
    public void Sanitisation_round_trips_through_persistence()
    {
        var first = SafeFileName.Create("My Holiday Photo (2).JPG");
        var second = SafeFileName.Create(first.Original);

        second.Value.Should().Be(first.Value);
        second.Original.Should().Be(first.Original);
    }

    // ── Storage key ──────────────────────────────────────────────────────

    /// <summary>
    /// Keys are generated, never accepted from a client — but the value object validates
    /// anyway, because the day it fires is the day something wrote a key past the generator.
    /// </summary>
    [Theory]
    [InlineData("../secrets")]
    [InlineData("/leading")]
    [InlineData("trailing/")]
    [InlineData("double//slash")]
    [InlineData("back\\slash")]
    public void A_storage_key_refuses_traversal_and_absolute_paths(string hostile)
    {
        StorageKey.TryCreate(hostile).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Every rendition of one asset shares a prefix, which is what makes deleting an asset one
    /// prefix delete rather than a fan-out over an unknown number of variants.
    /// </summary>
    [Fact]
    public void Every_rendition_of_an_asset_shares_one_prefix()
    {
        var assetId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

        var original = StorageKey.ForOriginal(
            MediaKind.Image, assetId, FileExtension.TryCreate("jpg").Value, createdAt);

        var thumbnail = StorageKey.ForVariant(
            original, MediaVariantPurpose.Thumbnail, FileExtension.TryCreate("jpg").Value);

        thumbnail.Prefix.Should().Be(original.Prefix);
        original.Value.Should().Contain("2026/07/27");
    }

    // ── Content hash ─────────────────────────────────────────────────────

    [Fact]
    public void The_same_bytes_hash_the_same_and_different_bytes_do_not()
    {
        var first = ContentHash.Compute("hello"u8);
        var second = ContentHash.Compute("hello"u8);
        var third = ContentHash.Compute("hellp"u8);

        first.Matches(second).Should().BeTrue();
        first.Matches(third).Should().BeFalse();
        first.Value.Should().HaveLength(ContentHash.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("zzzz1111111111111111111111111111111111111111111111111111111111zz")]
    public void A_malformed_digest_is_refused(string input)
    {
        ContentHash.TryCreate(input).IsFailure.Should().BeTrue();
    }

    // ── Dimensions ───────────────────────────────────────────────────────

    /// <summary>
    /// A 30 KB PNG declaring 60 000 × 60 000 costs fourteen gigabytes to decode. The only place
    /// to refuse it is before decoding, from the header.
    /// </summary>
    [Fact]
    public void Dimensions_beyond_the_decompression_ceiling_are_refused()
    {
        MediaDimensions.TryCreate(60_000, 60_000).IsFailure.Should().BeTrue();
        MediaDimensions.TryCreate(0, 100).IsFailure.Should().BeTrue();
        MediaDimensions.TryCreate(4000, 3000).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Upscaling a 96-pixel avatar to a 1280-pixel "large" variant produces something bigger,
    /// blurrier and more expensive than the original.
    /// </summary>
    [Fact]
    public void Scaling_never_enlarges()
    {
        var small = MediaDimensions.Create(96, 96);

        small.ScaledToFit(1280).Should().Be(small);
    }

    [Fact]
    public void Scaling_preserves_aspect_ratio()
    {
        var scaled = MediaDimensions.Create(4000, 2000).ScaledToFit(600);

        scaled.Width.Should().Be(600);
        scaled.Height.Should().Be(300);
    }

    // ── Media owner and reference ────────────────────────────────────────

    /// <summary>
    /// Half an owner is a caller who believes they attached the upload to something and has
    /// not.
    /// </summary>
    [Fact]
    public void An_owner_type_without_an_identifier_is_refused()
    {
        MediaOwner.TryCreate(MediaOwnerType.HousingListing, Guid.Empty)
            .IsFailure.Should().BeTrue();

        MediaOwner.TryCreate(MediaOwnerType.Unassigned, Guid.NewGuid())
            .IsFailure.Should().BeTrue();

        MediaOwner.TryCreate(MediaOwnerType.Unassigned, Guid.Empty)
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// The whole point of the reference contract is that <c>Cover</c>, <c>cover</c> and
    /// <c>coverImage</c> stop being three different things.
    /// </summary>
    [Theory]
    [InlineData("Cover", "cover")]
    [InlineData("COVER IMAGE", "cover-image")]
    [InlineData("  gallery  ", "gallery")]
    [InlineData("", "attachment")]
    public void A_reference_role_is_normalised(string input, string expected)
    {
        var reference = MediaReference.Create(
            Diaspora.SharedKernel.Identity.MediaId.New(),
            MediaOwner.Create(MediaOwnerType.HousingListing, Guid.NewGuid()),
            input);

        reference.Role.Should().Be(expected);
    }

    // ── Scan result ──────────────────────────────────────────────────────

    /// <summary>
    /// The most important assertion in this file. A scanner that errored has told us nothing,
    /// and treating nothing as clean is how a scanner outage becomes a distribution window.
    /// </summary>
    [Fact]
    public void A_failed_scan_is_not_treated_as_a_clean_one()
    {
        var now = DateTimeOffset.UtcNow;

        VirusScanResult.Clean("test", now).IsSafeToServe.Should().BeTrue();
        VirusScanResult.Skipped(now).IsSafeToServe.Should().BeTrue();

        VirusScanResult.Failed("test", "timeout", now).IsSafeToServe.Should().BeFalse();
        VirusScanResult.Infected("test", "EICAR", now).IsSafeToServe.Should().BeFalse();
        VirusScanResult.NotScanned.IsSafeToServe.Should().BeFalse();

        VirusScanResult.Infected("test", "EICAR", now).RequiresQuarantine.Should().BeTrue();
        VirusScanResult.Failed("test", "timeout", now).RequiresQuarantine.Should().BeFalse();
    }

    // ── Transformations ──────────────────────────────────────────────────

    /// <summary>
    /// An endpoint accepting an arbitrary width is one where an attacker mints unbounded
    /// renditions of a fifteen-megabyte photograph with a query string.
    /// </summary>
    [Fact]
    public void A_transformation_is_bounded_on_every_axis()
    {
        ImageTransformation.TryCreate(4, ResizeMode.Contain).IsFailure.Should().BeTrue();
        ImageTransformation.TryCreate(99_999, ResizeMode.Contain).IsFailure.Should().BeTrue();
        ImageTransformation.TryCreate(600, ResizeMode.Contain, quality: 5).IsFailure.Should().BeTrue();
        ImageTransformation.TryCreate(600, ResizeMode.Contain).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Rasterising untrusted XML is the one code path this module refuses to have.
    /// </summary>
    [Fact]
    public void A_vector_image_is_never_a_rendition_format()
    {
        ImageTransformation.TryCreate(600, ResizeMode.Contain, ImageFormat.Svg)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Two requests for the same transformation must resolve to the same stored object, which
    /// is only decidable if the key is deterministic.
    /// </summary>
    [Fact]
    public void A_transformation_cache_key_is_deterministic()
    {
        var first = ImageTransformation.ForPurpose(MediaVariantPurpose.Medium).Value;
        var second = ImageTransformation.ForPurpose(MediaVariantPurpose.Medium).Value;

        first.ToCacheKey().Should().Be(second.ToCacheKey());
        first.Should().Be(second);

        ImageTransformation.ForPurpose(MediaVariantPurpose.Thumbnail).Value.ToCacheKey()
            .Should().NotBe(first.ToCacheKey());
    }

    // ── Byte size ────────────────────────────────────────────────────────

    [Fact]
    public void Sizes_compare_and_render_in_units_a_member_can_read()
    {
        ByteSize.FromMegabytes(5).Bytes.Should().Be(5 * 1024 * 1024);
        ByteSize.FromMegabytes(18).Exceeds(ByteSize.FromMegabytes(15)).Should().BeTrue();
        ByteSize.FromBytes(512).ToString().Should().Be("512 B");
        ByteSize.FromMegabytes(5).ToString().Should().Be("5 MB");
    }
}
