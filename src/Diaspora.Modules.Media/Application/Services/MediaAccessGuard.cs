using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// The one place a read or write of an asset is authorised.
/// <para>
/// Every path that serves bytes goes through here — the content endpoint, the signed-link
/// redemption, the metadata read, the variant read. One place, because an access rule with
/// four implementations has four chances to be subtly different, and the fourth is always the
/// one an auditor finds.
/// </para>
/// <para>
/// <b>Two questions, not one, and separating them is what makes a signed link work.</b>
/// </para>
/// <list type="bullet">
/// <item><b>State</b> — is this asset servable at all? Deleted, quarantined and unscanned files
/// are served to nobody, whatever authority the caller presents. <see cref="CanServeBytes"/>.</item>
/// <item><b>Identity</b> — may <em>this</em> caller have it? Ownership, grants, visibility, and
/// the owning vertical's opinion. <see cref="CanReadContentAsync"/>.</item>
/// </list>
/// <para>
/// A signed token establishes identity and nothing else, so redemption asks the first question
/// and skips the second — see <see cref="CanServeSignedAsync"/>. Conflating the two is what had
/// the content endpoint answer 403 to a perfectly valid signed URL: it verified the signature and
/// then demanded the caller prove who they were all over again, which an <c>&lt;img&gt;</c> tag
/// cannot do.
/// </para>
/// <para>
/// <b>The layering, in order:</b>
/// </para>
/// <list type="number">
/// <item>Deleted or quarantined: refused outright. Nothing overrides this — not a staff role,
/// not a vertical's policy, not a signed link.</item>
/// <item>Staff with <c>media:administer</c>: allowed to read a record, so a moderator can look at
/// what was flagged. Deliberately after the quarantine check for reading <em>content</em>: staff
/// read quarantined metadata, never quarantined bytes.</item>
/// <item>Media's own rules: uploader, explicit grant, visibility, readiness.</item>
/// <item>The owning vertical's policy, which may deny, or widen a read.</item>
/// </list>
/// <para>
/// <b>Staff never pass the management check.</b> <c>media:administer</c> widens what a moderator
/// may <em>see</em>, and nothing else: editing, deleting, attaching and arranging a member's files
/// are the member's alone, and <see cref="CanManageAsync"/> answers a moderator exactly as it
/// answers any other member who did not upload the file. Until 7 Nov 2026 it let anyone holding the
/// permission delete, edit, re-attach or reorder any member's files through the member's own
/// routes, with no reason and no notice. Staff now take a file down through
/// <c>POST /media/{id}/moderation</c>, which requires a reason and records it.
/// </para>
/// </summary>
public sealed class MediaAccessGuard
{
    private readonly MediaAccessPolicyRegistry _policies;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly MediaSecurityOptions _options;

    public MediaAccessGuard(
        MediaAccessPolicyRegistry policies,
        ICurrentUser currentUser,
        IClock clock,
        IOptions<MediaSecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _policies = policies;
        _currentUser = currentUser;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// Whether the caller is staff who may <em>see</em> any asset's record. Grants no management.
    /// </summary>
    public bool IsAdministrator =>
        _currentUser.Permissions.Contains(MediaPermissions.Administer, StringComparer.Ordinal);

    /// <summary>
    /// Whether the caller may read this asset's record — its metadata, its variant list.
    /// <para>
    /// Distinct from reading its bytes. A moderator investigating a quarantine needs to see
    /// what was uploaded, by whom and when, without the platform handing them the malware.
    /// </para>
    /// </summary>
    public async Task<Result> CanReadMetadataAsync(
        MediaAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.Status is MediaAssetStatus.Deleted)
        {
            return Result.Failure(MediaErrors.AssetDeleted);
        }

        if (IsAdministrator)
        {
            return Result.Success();
        }

        return await EvaluateAsync(asset, MediaAccessLevel.Read, cancellationToken);
    }

    /// <summary>
    /// Whether this asset's bytes may be served to <b>anyone at all</b>, before the question of
    /// who is asking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The floor that no credential lifts. Quarantined bytes are served to nobody, staff
    /// included — a moderation UI that renders a flagged file is a moderation UI that runs the
    /// payload in a moderator's browser.
    /// </para>
    /// <para>
    /// "Not scanned" is not "clean". A scanner that has not run, or that errored, has told us
    /// nothing, and treating nothing as safe is how a scanner outage becomes a distribution
    /// window. The uploader still sees their own file, which is what makes an upload feel
    /// immediate without exposing it to anyone else — so this takes the caller it should judge
    /// that allowance against, rather than reading the ambient one. On the signed path the two
    /// differ: the request arrives with no identity at all, and the token's audience is who the
    /// bytes were minted for.
    /// </para>
    /// </remarks>
    public Result CanServeBytes(MediaAsset asset, UserId? onBehalfOf)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.Status is MediaAssetStatus.Quarantined)
        {
            return Result.Failure(MediaErrors.AssetQuarantined);
        }

        if (asset.Status is MediaAssetStatus.Deleted)
        {
            return Result.Failure(MediaErrors.AssetDeleted);
        }

        var uploadedByCaller = onBehalfOf is not null && asset.UploadedBy == onBehalfOf;

        if (!asset.ScanResult.IsSafeToServe && !_options.AllowUnscannedReads && !uploadedByCaller)
        {
            return Result.Failure(MediaErrors.AssetNotReady);
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether bytes may be served to the holder of a <b>verified</b> signed token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The signature has already established who the caller is — that is the entire job of the
    /// token, and re-deriving it from the request would defeat the purpose, because the request
    /// that redeems a signed URL is an image load with no <c>Authorization</c> header on it.
    /// </para>
    /// <para>
    /// What a signature cannot vouch for is anything that happened <em>after</em> it was minted,
    /// and that is what is still checked here: the asset may have been quarantined or deleted
    /// since, and the owning vertical may have withdrawn the thing it was attached to. Both
    /// revoke a live link, which is the property a provider-presigned URL cannot offer and the
    /// reason this module mints its own.
    /// </para>
    /// </remarks>
    public async Task<Result> CanServeSignedAsync(
        MediaAsset asset, UserId? audience, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var servable = CanServeBytes(asset, audience);

        if (servable.IsFailure)
        {
            return servable;
        }

        var decision = await AskOwningVerticalAsync(
            asset, audience, MediaAccessLevel.Read, cancellationToken);

        return decision.IsDenial
            ? Result.Failure(MediaErrors.AssetAccessDenied)
            : Result.Success();
    }

    /// <summary>
    /// Whether the caller may read this asset's bytes on their own authority.
    /// </summary>
    public async Task<Result> CanReadContentAsync(
        MediaAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var servable = CanServeBytes(asset, _currentUser.UserId);

        if (servable.IsFailure)
        {
            return servable;
        }

        return await EvaluateAsync(asset, MediaAccessLevel.Read, cancellationToken);
    }

    /// <summary>
    /// Whether the caller may change or delete this asset.
    /// <para>
    /// The uploader, or somebody the uploader granted management to. Never staff on the strength
    /// of a staff permission: a moderator acts on a member's file through the moderation route,
    /// which takes a reason, and not through the member's own edit and delete routes.
    /// </para>
    /// </summary>
    public async Task<Result> CanManageAsync(
        MediaAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.Status is MediaAssetStatus.Deleted)
        {
            return Result.Failure(MediaErrors.AssetDeleted);
        }

        // Ownership is checked here as well as inside `EvaluateAsync`, and deliberately so: this
        // is what makes it impossible for a vertical's policy to hand management of a file to
        // someone who does not own it. A policy can widen a read; it can never widen this.
        if (!asset.CanBeManagedBy(_currentUser.UserId, _clock.UtcNow))
        {
            return Result.Failure(MediaErrors.OwnershipRequired);
        }

        return await EvaluateAsync(asset, MediaAccessLevel.Manage, cancellationToken);
    }

    /// <summary>
    /// Media's own decision, then the owning vertical's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The vertical is asked only when the asset is actually attached to something and only for
    /// non-public assets. Asking it about a public avatar would put a repository call on the hot
    /// path of every image in every list.
    /// </para>
    /// <para>
    /// <b>A vertical may widen a read, and only a read.</b> This is the half that was declared and
    /// never wired: <c>MediaAccessDecision.Allow</c> documented itself as "the vertical grants
    /// access on its own authority" while this method only ever looked at
    /// <c>IsDenial</c> — so <c>Allow</c> and <c>Abstain</c> were the same value and no vertical
    /// could make anything readable.
    /// </para>
    /// <para>
    /// The consequence was not theoretical. A marketplace or housing photograph is attached to a
    /// listing and, like every asset, created <c>Private</c>; Media therefore refused it to
    /// everyone except the member who uploaded it. Every buyer and every prospective tenant saw a
    /// "no photograph" panel — on listings that <em>cannot be published without one</em>.
    /// </para>
    /// <para>
    /// Widening is safe precisely because of what it cannot reach. The state floor has already
    /// run in <see cref="CanServeBytes"/> and is not revisited here, so no policy can serve a
    /// quarantined or deleted file; management is gated on ownership before this is called; and a
    /// denial still wins outright. What a policy can do is answer the one question Media cannot:
    /// whether the thing this file is attached to is something the caller may look at.
    /// </para>
    /// </remarks>
    private async Task<Result> EvaluateAsync(
        MediaAsset asset, MediaAccessLevel level, CancellationToken cancellationToken)
    {
        var caller = _currentUser.UserId;

        var mediaSaysYes = level is MediaAccessLevel.Read
            ? asset.CanBeReadBy(caller, _clock.UtcNow)
            : asset.CanBeManagedBy(caller, _clock.UtcNow);

        if (!asset.Owner.IsAssigned || asset.Visibility is MediaVisibility.Public)
        {
            // Nothing to ask: an unattached asset has no vertical, and a public one is already
            // readable by everyone the floor admits.
            return mediaSaysYes
                ? Result.Success()
                : Result.Failure(MediaErrors.AssetAccessDenied);
        }

        var decision = await AskOwningVerticalAsync(asset, caller, level, cancellationToken);

        if (decision.IsDenial)
        {
            return Result.Failure(MediaErrors.AssetAccessDenied);
        }

        if (mediaSaysYes)
        {
            return Result.Success();
        }

        // Media would refuse — on visibility alone, since the state floor has already passed.
        // A vertical that positively grants overrides that for a read.
        return level is MediaAccessLevel.Read && decision.Allowed is true
            ? Result.Success()
            : Result.Failure(MediaErrors.AssetAccessDenied);
    }

    private Task<MediaAccessDecision> AskOwningVerticalAsync(
        MediaAsset asset,
        UserId? caller,
        MediaAccessLevel level,
        CancellationToken cancellationToken)
    {
        if (!asset.Owner.IsAssigned)
        {
            return Task.FromResult(MediaAccessDecision.Abstain());
        }

        return _policies.EvaluateAsync(
            new MediaAccessRequest
            {
                Caller = caller,
                MediaId = asset.Id,
                OwnerType = asset.Owner.Type,
                OwnerId = asset.Owner.Id,
                Role = asset.Role,
                RequestedLevel = level,
                UploadedBy = asset.UploadedBy
            },
            cancellationToken);
    }
}
