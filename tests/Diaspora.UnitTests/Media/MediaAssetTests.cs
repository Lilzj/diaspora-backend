using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Events;
using Diaspora.Modules.Media;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The asset aggregate's invariants.
/// <para>
/// The one this file exists for: <b>bytes are unreadable until they have been proven safe.</b>
/// There is no setter on <c>Status</c> and no route to <c>Ready</c> except through
/// <c>MarkReady</c>, which refuses unless the scan says so. Not because a caller would publish
/// an unscanned file deliberately, but because in a codebase with several upload paths one of
/// them will eventually forget to check.
/// </para>
/// </summary>
public sealed class MediaAssetTests
{
    private static readonly UserId Uploader = UserId.New();

    // ── Creation ─────────────────────────────────────────────────────────

    [Fact]
    public void A_new_asset_is_private_and_unpublished()
    {
        var asset = Record()!;

        asset.Status.Should().Be(MediaAssetStatus.Pending);
        asset.Visibility.Should().Be(MediaVisibility.Private);
        asset.ScanResult.Status.Should().Be(VirusScanStatus.NotScanned);
        asset.Owner.IsAssigned.Should().BeFalse();

        asset.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MediaAssetCreatedDomainEvent>();
    }

    [Fact]
    public void An_empty_file_is_refused()
    {
        Record(size: 0).Should().BeNull();
    }

    [Fact]
    public void A_file_over_its_kinds_ceiling_is_refused()
    {
        // The verification ceiling is 5 MB, which the client advertises on screen.
        var oversized = MediaAsset.Record(
            MediaId.New(),
            Uploader,
            MediaKind.VerificationDocument,
            Location(),
            MediaContentType.Create("application/pdf"),
            SafeFileName.Create("passport.pdf"),
            ByteSize.FromMegabytes(6),
            ContentHash.Compute("x"u8));

        oversized.IsFailure.Should().BeTrue();
        oversized.Error.Code.Should().Be(MediaErrorCodes.UploadTooLarge);
    }

    // ── The publication gate ─────────────────────────────────────────────

    /// <summary>The single most important assertion in the module.</summary>
    [Fact]
    public void An_unscanned_asset_cannot_be_published()
    {
        var asset = Record()!;

        asset.MarkReady().IsFailure.Should().BeTrue();
        asset.Status.Should().NotBe(MediaAssetStatus.Ready);
    }

    /// <summary>
    /// A scanner that errored has told us nothing. Treating nothing as clean is how a scanner
    /// outage becomes a malware distribution window.
    /// </summary>
    [Fact]
    public void An_asset_whose_scan_failed_cannot_be_published()
    {
        var asset = Record()!;

        asset.RecordScan(VirusScanResult.Failed("clam", "timeout", DateTimeOffset.UtcNow));

        asset.MarkReady().IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// A deployment with no scanner is a visible, explicit choice — reported by the readiness
    /// check — rather than a reason nobody can upload anything.
    /// </summary>
    [Fact]
    public void A_skipped_scan_still_permits_publication()
    {
        var asset = Record()!;

        asset.RecordScan(VirusScanResult.Skipped(DateTimeOffset.UtcNow));

        asset.MarkReady().IsSuccess.Should().BeTrue();
        asset.Status.Should().Be(MediaAssetStatus.Ready);
    }

    [Fact]
    public void A_clean_asset_publishes_and_announces_itself()
    {
        var asset = Record()!;
        asset.ClearDomainEvents();

        asset.RecordScan(VirusScanResult.Clean("clam", DateTimeOffset.UtcNow));
        asset.MarkReady().IsSuccess.Should().BeTrue();

        asset.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MediaAssetReadyDomainEvent>();
    }

    // ── Quarantine ───────────────────────────────────────────────────────

    [Fact]
    public void An_infected_asset_is_quarantined_forced_private_and_announced()
    {
        var asset = Record()!;
        asset.ChangeVisibility(MediaVisibility.Public);
        asset.ClearDomainEvents();

        asset.RecordScan(
            VirusScanResult.Infected("clam", "EICAR-Test", DateTimeOffset.UtcNow));

        asset.Status.Should().Be(MediaAssetStatus.Quarantined);
        asset.Visibility.Should().Be(MediaVisibility.Private);

        asset.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MediaAssetQuarantinedDomainEvent>();
    }

    /// <summary>
    /// Nobody reads quarantined bytes — not the uploader, not staff. A moderation UI that
    /// rendered a flagged file would run the payload in a moderator's browser.
    /// </summary>
    [Fact]
    public void A_quarantined_asset_is_unreadable_by_everyone_including_its_uploader()
    {
        var asset = Record()!;

        asset.RecordScan(VirusScanResult.Infected("clam", "EICAR", DateTimeOffset.UtcNow));

        asset.CanBeReadBy(Uploader, DateTimeOffset.UtcNow).Should().BeFalse();
        asset.CanBeReadBy(UserId.New(), DateTimeOffset.UtcNow).Should().BeFalse();
        asset.CanBeReadBy(null, DateTimeOffset.UtcNow).Should().BeFalse();

        asset.MarkReady().IsFailure.Should().BeTrue();
    }

    // ── Access ───────────────────────────────────────────────────────────

    /// <summary>
    /// What makes an upload feel instant to the person who made it, without exposing an
    /// unscanned file to anyone else.
    /// </summary>
    [Fact]
    public void The_uploader_sees_their_own_file_while_it_is_still_processing()
    {
        var asset = Record()!;

        asset.CanBeReadBy(Uploader, DateTimeOffset.UtcNow).Should().BeTrue();
        asset.CanBeReadBy(UserId.New(), DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void A_public_asset_is_readable_anonymously_once_ready()
    {
        var asset = Ready();
        asset.ChangeVisibility(MediaVisibility.Public);

        asset.CanBeReadBy(null, DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void An_authenticated_asset_is_not_readable_anonymously()
    {
        var asset = Ready();
        asset.ChangeVisibility(MediaVisibility.Authenticated);

        asset.CanBeReadBy(null, DateTimeOffset.UtcNow).Should().BeFalse();
        asset.CanBeReadBy(UserId.New(), DateTimeOffset.UtcNow).Should().BeTrue();
    }

    /// <summary>
    /// The verification-review case: a reviewer reads exactly this document, for exactly as
    /// long as the review takes.
    /// </summary>
    [Fact]
    public void An_explicit_grant_expires_on_its_own()
    {
        var asset = Ready();
        var reviewer = UserId.New();
        var now = DateTimeOffset.UtcNow;

        asset.Grant(reviewer, MediaAccessLevel.Read, now.AddMinutes(10), Uploader);

        asset.CanBeReadBy(reviewer, now.AddMinutes(5)).Should().BeTrue();
        asset.CanBeReadBy(reviewer, now.AddMinutes(15)).Should().BeFalse();
    }

    [Fact]
    public void A_revoked_grant_stops_working_immediately()
    {
        var asset = Ready();
        var reviewer = UserId.New();
        var now = DateTimeOffset.UtcNow;

        asset.Grant(reviewer, MediaAccessLevel.Read, null, Uploader);
        asset.CanBeReadBy(reviewer, now).Should().BeTrue();

        asset.Revoke(reviewer);
        asset.CanBeReadBy(reviewer, now).Should().BeFalse();

        // Kept rather than deleted: "who could see this, and when did that stop" is a question
        // a data-protection request will eventually ask.
        asset.Grants.Should().ContainSingle().Which.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Access_levels_are_ordered()
    {
        var asset = Ready();
        var editor = UserId.New();
        var now = DateTimeOffset.UtcNow;

        asset.Grant(editor, MediaAccessLevel.Manage, null, Uploader);

        asset.CanBeReadBy(editor, now).Should().BeTrue();
        asset.CanBeManagedBy(editor, now).Should().BeTrue();
    }

    // ── Visibility rules ─────────────────────────────────────────────────

    /// <summary>
    /// Not a default a caller can override. It is the reason Trust was willing to hand its
    /// file storage to another module.
    /// </summary>
    [Fact]
    public void A_verification_document_cannot_be_made_public()
    {
        var asset = MediaAsset.Record(
            MediaId.New(),
            Uploader,
            MediaKind.VerificationDocument,
            Location(),
            MediaContentType.Create("application/pdf"),
            SafeFileName.Create("permit.pdf"),
            ByteSize.FromMegabytes(1),
            ContentHash.Compute("x"u8)).Value;

        asset.ChangeVisibility(MediaVisibility.Public).IsFailure.Should().BeTrue();
        asset.Visibility.Should().Be(MediaVisibility.Private);
        asset.RequiresSignedAccess.Should().BeTrue();
        asset.SignedUrlLifetime.Should().Be(TimeSpan.FromMinutes(10));
    }

    // ── Ownership ────────────────────────────────────────────────────────

    /// <summary>
    /// An asset that could be moved between owners is one whose access rules change under the
    /// module relying on them.
    /// </summary>
    [Fact]
    public void An_asset_cannot_be_re_attached_to_a_different_owner()
    {
        var asset = Record()!;
        var listing = MediaOwner.Create(MediaOwnerType.HousingListing, Guid.NewGuid());

        asset.AttachTo(listing, "cover").IsSuccess.Should().BeTrue();
        asset.Role.Should().Be("cover");

        var other = MediaOwner.Create(MediaOwnerType.MarketplaceListing, Guid.NewGuid());
        asset.AttachTo(other, "cover").IsFailure.Should().BeTrue();

        // Re-attaching to the same owner is idempotent, so a retried request is not an error.
        asset.AttachTo(listing, "gallery").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Attaching_announces_that_the_asset_is_no_longer_an_orphan()
    {
        var asset = Record()!;
        asset.ClearDomainEvents();

        asset.AttachTo(
            MediaOwner.Create(MediaOwnerType.HousingListing, Guid.NewGuid()), "cover");

        asset.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MediaAssetAttachedDomainEvent>();
    }

    // ── Variants ─────────────────────────────────────────────────────────

    /// <summary>
    /// Re-running the pipeline after a codec upgrade should improve the thumbnail, not leave
    /// two of them with no rule for which is served.
    /// </summary>
    [Fact]
    public void A_second_variant_of_the_same_purpose_replaces_the_first()
    {
        var asset = Record()!;

        asset.AddVariant(
            MediaVariantPurpose.Thumbnail, Location(), MediaContentType.Create("image/jpeg"),
            ByteSize.FromBytes(1000), MediaDimensions.Create(96, 96), "v1");

        asset.AddVariant(
            MediaVariantPurpose.Thumbnail, Location(), MediaContentType.Create("image/webp"),
            ByteSize.FromBytes(700), MediaDimensions.Create(96, 96), "v2");

        asset.Variants.Should().ContainSingle();
        asset.FindVariant(MediaVariantPurpose.Thumbnail)!.TransformationKey.Should().Be("v2");
    }

    /// <summary>The original is not a rendition; it is the thing renditions come from.</summary>
    [Fact]
    public void The_original_cannot_be_added_as_a_variant()
    {
        var asset = Record()!;

        asset.AddVariant(
            MediaVariantPurpose.Original, Location(), MediaContentType.Create("image/jpeg"),
            ByteSize.FromBytes(1), null, null)
            .IsFailure.Should().BeTrue();
    }

    // ── Deletion ─────────────────────────────────────────────────────────

    [Fact]
    public void Deleting_is_soft_and_makes_the_asset_unreadable()
    {
        var asset = Ready();
        asset.ChangeVisibility(MediaVisibility.Public);
        asset.ClearDomainEvents();

        asset.Delete(Uploader).IsSuccess.Should().BeTrue();

        asset.Status.Should().Be(MediaAssetStatus.Deleted);
        asset.CanBeReadBy(Uploader, DateTimeOffset.UtcNow).Should().BeFalse();
        asset.CanBeReadBy(null, DateTimeOffset.UtcNow).Should().BeFalse();

        asset.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MediaAssetDeletedDomainEvent>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static MediaAsset? Record(long size = 1024) =>
        MediaAsset.Record(
            MediaId.New(),
            Uploader,
            MediaKind.Image,
            Location(),
            MediaContentType.Create("image/jpeg"),
            SafeFileName.Create("photo.jpg"),
            ByteSize.FromBytes(size),
            ContentHash.Compute("photo"u8)) is { IsSuccess: true } result
            ? result.Value
            : null;

    private static MediaAsset Ready()
    {
        var asset = Record()!;
        asset.RecordScan(VirusScanResult.Clean("test", DateTimeOffset.UtcNow));
        asset.MarkReady();
        asset.ClearDomainEvents();
        return asset;
    }

    private static StorageLocation Location() =>
        StorageLocation.Create(
            "filesystem",
            "diaspora-media",
            StorageKey.ForOriginal(
                MediaKind.Image,
                Guid.NewGuid(),
                FileExtension.TryCreate("jpg").Value,
                DateTimeOffset.UtcNow));
}
