using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
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
/// <b>The layering, in order, and every layer can only narrow:</b>
/// </para>
/// <list type="number">
/// <item>Deleted or quarantined: refused outright. Nothing overrides this — not a staff role,
/// not a vertical's policy, not a signed link.</item>
/// <item>Staff with <c>media:administer</c>: allowed, so a moderator can look at what was
/// flagged. Deliberately after the quarantine check for reading <em>content</em>: staff read
/// quarantined metadata, never quarantined bytes.</item>
/// <item>Media's own rules: uploader, explicit grant, visibility, readiness.</item>
/// <item>The owning vertical's policy: may only deny or abstain.</item>
/// </list>
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

    /// <summary>Whether the caller is staff who may see any asset.</summary>
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
    /// Whether the caller may read this asset's bytes.
    /// <para>
    /// Stricter than metadata in two ways that matter. Quarantined bytes are served to nobody,
    /// staff included — a moderation UI that renders a flagged file is a moderation UI that
    /// runs the payload in a moderator's browser. And an unscanned asset is refused unless the
    /// deployment has explicitly opted into serving unscanned content, which is a development
    /// convenience the readiness endpoint reports.
    /// </para>
    /// </summary>
    public async Task<Result> CanReadContentAsync(
        MediaAsset asset, CancellationToken cancellationToken = default)
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

        // "Not scanned" is not "clean". A scanner that has not run, or errored, has told us
        // nothing, and treating nothing as safe is how a scanner outage becomes a distribution
        // window. The uploader still sees their own file, which is what makes an upload feel
        // immediate without exposing it to anyone else.
        var scanned = asset.ScanResult.IsSafeToServe;
        var ownedByCaller = _currentUser.UserId is not null && asset.UploadedBy == _currentUser.UserId;

        if (!scanned && !_options.AllowUnscannedReads && !ownedByCaller)
        {
            return Result.Failure(MediaErrors.AssetNotReady);
        }

        return await EvaluateAsync(asset, MediaAccessLevel.Read, cancellationToken);
    }

    /// <summary>Whether the caller may change or delete this asset.</summary>
    public async Task<Result> CanManageAsync(
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

        if (!asset.CanBeManagedBy(_currentUser.UserId, _clock.UtcNow))
        {
            return Result.Failure(MediaErrors.OwnershipRequired);
        }

        return await EvaluateAsync(asset, MediaAccessLevel.Manage, cancellationToken);
    }

    /// <summary>
    /// Media's own decision, then the owning vertical's.
    /// <para>
    /// The vertical is asked only when the asset is actually attached to something and only
    /// for non-public assets. Asking it about a public avatar would put a repository call on
    /// the hot path of every image in every list.
    /// </para>
    /// </summary>
    private async Task<Result> EvaluateAsync(
        MediaAsset asset, MediaAccessLevel level, CancellationToken cancellationToken)
    {
        var caller = _currentUser.UserId;

        var mediaSaysYes = level is MediaAccessLevel.Read
            ? asset.CanBeReadBy(caller, _clock.UtcNow)
            : asset.CanBeManagedBy(caller, _clock.UtcNow);

        if (!mediaSaysYes)
        {
            return Result.Failure(MediaErrors.AssetAccessDenied);
        }

        if (!asset.Owner.IsAssigned || asset.Visibility is MediaVisibility.Public)
        {
            return Result.Success();
        }

        var decision = await _policies.EvaluateAsync(
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

        // A vertical can only narrow. It cannot grant access Media already refused — that
        // check has already happened above — and its abstention leaves Media's answer intact.
        return decision.IsDenial
            ? Result.Failure(MediaErrors.AssetAccessDenied)
            : Result.Success();
    }
}
