using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Options;
using Diaspora.Modules.Media;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// Who gets the bytes.
/// </summary>
/// <remarks>
/// <para>
/// Written after UAT-121, in which <b>no listing photograph on the platform was visible to
/// anyone</b> — not to a buyer, not to a prospective tenant, and not even to the seller who
/// uploaded it, on a listing that could not have been published without a photograph. The guard
/// had no tests at all; 188 media tests passed throughout.
/// </para>
/// <para>
/// Two rules are load-bearing here and each is asserted from both directions. A vertical may
/// <em>widen</em> a read — that is the fix. A vertical may not widen anything else: not past
/// quarantine, not past deletion, not past an unscanned file, and not into management. A test
/// that only proved the first would have shipped a hole.
/// </para>
/// </remarks>
public sealed class MediaAccessGuardTests
{
    private static readonly UserId Uploader = UserId.New();
    private static readonly UserId Stranger = UserId.New();
    private static readonly Guid Listing = Guid.NewGuid();

    // ── The defect: a published listing's photograph ────────────────────────

    [Fact]
    public async Task Without_a_policy_a_stranger_cannot_read_an_attached_private_asset()
    {
        // Media's own default, and the correct one. This is the state every listing photograph
        // was in: attached, private, and refused to everybody but its uploader.
        var guard = Guard(caller: Stranger);

        var result = await guard.CanReadContentAsync(AttachedAsset());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetAccessDenied);
    }

    [Fact]
    public async Task A_vertical_that_grants_makes_the_photograph_readable()
    {
        // The whole of UAT-121 in one assertion. `Allow` was declared, documented as "the
        // vertical grants access on its own authority", and never read — the guard only ever
        // looked at `IsDenial`, so `Allow` and `Abstain` were the same value.
        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Allow()));

        var result = await guard.CanReadContentAsync(AttachedAsset());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_abstention_leaves_the_refusal_standing()
    {
        // The distinction that makes `Allow` meaningful: a policy with no opinion must not read
        // as a grant, or every vertical would silently publish everything it owns.
        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Abstain()));

        var result = await guard.CanReadContentAsync(AttachedAsset());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task A_denial_is_final_even_for_the_uploader()
    {
        // Deny still wins outright. A vertical that has withdrawn something can take its images
        // down from under the member who uploaded them.
        var guard = Guard(
            caller: Uploader,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Deny()));

        var result = await guard.CanReadContentAsync(AttachedAsset());

        result.IsFailure.Should().BeTrue();
    }

    // ── What a grant cannot reach ───────────────────────────────────────────

    [Fact]
    public async Task A_grant_cannot_serve_a_quarantined_file()
    {
        var asset = AttachedAsset();
        asset.RecordScan(VirusScanResult.Infected("test", "EICAR", DateTimeOffset.UnixEpoch));

        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Allow()));

        var result = await guard.CanReadContentAsync(asset);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetQuarantined);
    }

    [Fact]
    public async Task A_grant_cannot_serve_an_unscanned_file()
    {
        // "Not scanned" is not "clean". A vertical saying the listing is public says nothing
        // about whether the bytes have been proven safe.
        var asset = Attach(NewAsset());

        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Allow()));

        var result = await guard.CanReadContentAsync(asset);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetNotReady);
    }

    [Fact]
    public async Task A_grant_cannot_hand_a_stranger_management_of_someone_elses_file()
    {
        // Seeing a listing is not a reason to be able to delete its pictures. Ownership is
        // checked before a policy is consulted, and the grant path is read-only besides.
        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Allow()));

        var result = await guard.CanManageAsync(AttachedAsset());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetOwnershipRequired);
    }

    [Fact]
    public async Task A_policy_for_a_different_owner_type_is_not_consulted()
    {
        // The registry resolves by owner type. A housing policy must not decide a marketplace
        // photograph, however permissive it is.
        var guard = Guard(
            caller: Stranger,
            policy: new FakePolicy(MediaOwnerType.HousingListing, MediaAccessDecision.Allow()));

        var result = await guard.CanReadContentAsync(AttachedAsset());

        result.IsFailure.Should().BeTrue();
    }

    // ── Redeeming a signed link ─────────────────────────────────────────────

    [Fact]
    public async Task A_verified_signature_serves_a_private_asset_to_an_anonymous_request()
    {
        // The second half of UAT-121. An `<img src>` sends no Authorization header, so the
        // request that redeems a signed URL is anonymous by construction. The guard used to
        // re-derive the caller from that request — nobody — and refuse, which meant every signed
        // URL for a private asset answered 403, including one the owner had just minted.
        var guard = Guard(caller: null);

        var result = await guard.CanServeSignedAsync(AttachedAsset(), audience: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_signature_does_not_survive_quarantine()
    {
        // What a signature cannot vouch for is what happened after it was minted. This is the
        // property a provider-presigned URL cannot offer, and the reason the module signs its own.
        var asset = AttachedAsset();
        asset.RecordScan(VirusScanResult.Infected("test", "EICAR", DateTimeOffset.UnixEpoch));

        var result = await Guard(caller: null).CanServeSignedAsync(asset, audience: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetQuarantined);
    }

    [Fact]
    public async Task A_signature_does_not_survive_the_vertical_withdrawing_the_listing()
    {
        var guard = Guard(
            caller: null,
            policy: new FakePolicy(MediaOwnerType.MarketplaceListing, MediaAccessDecision.Deny()));

        var result = await guard.CanServeSignedAsync(AttachedAsset(), audience: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MediaErrorCodes.AssetAccessDenied);
    }

    [Fact]
    public async Task An_audience_bound_token_carries_the_uploaders_unscanned_allowance()
    {
        // A member who has just uploaded a photograph sees it before the scanner has run. That
        // allowance has to follow the token rather than the request, because the request that
        // fetches the image has no identity on it at all.
        var asset = Attach(NewAsset());

        var servable = Guard(caller: null).CanServeBytes(asset, Uploader);
        var refused = Guard(caller: null).CanServeBytes(asset, Stranger);

        servable.IsSuccess.Should().BeTrue();
        refused.IsFailure.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MediaAccessGuard Guard(UserId? caller, IMediaAccessPolicy? policy = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(caller);
        currentUser.Permissions.Returns([]);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch.AddYears(56));

        return new MediaAccessGuard(
            new MediaAccessPolicyRegistry(policy is null ? [] : [policy]),
            currentUser,
            clock,
            Options.Create(new MediaSecurityOptions()));
    }

    /// <summary>A scanned, ready, private photograph attached to a marketplace listing.</summary>
    private static MediaAsset AttachedAsset()
    {
        var asset = NewAsset();
        asset.RecordScan(VirusScanResult.Clean("test", DateTimeOffset.UnixEpoch));
        asset.MarkReady();
        return Attach(asset);
    }

    private static MediaAsset Attach(MediaAsset asset)
    {
        asset.AttachTo(
            MediaOwner.Create(MediaOwnerType.MarketplaceListing, Listing),
            MediaReferenceRoles.Gallery);

        return asset;
    }

    private static MediaAsset NewAsset() =>
        MediaAsset.Record(
            MediaId.New(),
            Uploader,
            MediaKind.Image,
            StorageLocation.Create(
                "filesystem",
                "diaspora-media",
                StorageKey.ForOriginal(
                    MediaKind.Image,
                    Guid.NewGuid(),
                    FileExtension.TryCreate("jpg").Value,
                    DateTimeOffset.UnixEpoch)),
            MediaContentType.Create("image/jpeg"),
            SafeFileName.Create("photo.jpg"),
            ByteSize.FromBytes(1024),
            ContentHash.Compute("photo"u8)).Value;
}
