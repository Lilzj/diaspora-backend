using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Events;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.Assets;

/// <summary>
/// One digital asset: the bytes a member uploaded, everything discovered about them, every
/// rendition derived from them, and who may read them.
/// <para>
/// The platform's single owner of a file. Every other module holds a
/// <see cref="MediaReference"/> — an identifier and a role — and asks this aggregate for a
/// link when it needs one. Nothing else on the platform stores a storage key, mints a URL, or
/// knows which provider a byte lives in.
/// </para>
/// <para>
/// <b>The invariant the whole aggregate exists to protect</b> is that bytes are unreadable
/// until they have been proven safe. An asset is created <see cref="MediaAssetStatus.Pending"/>
/// and <see cref="MediaVisibility.Private"/>; it becomes readable only through
/// <see cref="MarkReady"/>, which refuses unless the scan result says so. There is no setter
/// on <see cref="Status"/> and no way to publish an unscanned file — not because a caller
/// would do it deliberately, but because in a codebase with a dozen upload paths one of them
/// will eventually forget to check.
/// </para>
/// <para>
/// Variants and grants are entities inside this boundary rather than aggregates of their own.
/// A variant has no life without its asset — deleting the asset deletes it, and nothing
/// queries variants across assets — and a grant is meaningless without the thing it grants
/// access to. Keeping both inside means "an asset has at most one variant per purpose" and
/// "a revoked grant cannot be used" are enforced in one place instead of at every call site.
/// </para>
/// </summary>
public sealed class MediaAsset : AggregateRoot<MediaId>, IAuditableEntity, ISoftDeletable
{
    public const int MaxAltTextLength = 500;

    public const int MaxCaptionLength = 1000;

    /// <summary>
    /// How many derived renditions one asset may hold. The fixed ladder is five rungs; the
    /// headroom is for a deployment that adds WebP and AVIF alongside them. A ceiling exists
    /// at all because a transformation endpoint without one is a storage-amplification bug.
    /// </summary>
    public const int MaxVariants = 16;

    private readonly List<MediaVariant> _variants = [];
    private readonly List<MediaGrant> _grants = [];

    public UserId UploadedBy { get; private set; } = default!;

    public MediaKind Kind { get; private set; }

    public MediaAssetStatus Status { get; private set; }

    public MediaVisibility Visibility { get; private set; }

    /// <summary>What the asset was uploaded for. <see cref="MediaOwner.Unassigned"/> until claimed.</summary>
    public MediaOwner Owner { get; private set; } = MediaOwner.Unassigned;

    /// <summary>The role the asset plays for its owner: <c>cover</c>, <c>gallery</c>, <c>avatar</c>.</summary>
    public string? Role { get; private set; }

    public StorageLocation Location { get; private set; } = default!;

    public MediaContentType ContentType { get; private set; } = default!;

    public SafeFileName FileName { get; private set; } = default!;

    public ByteSize Size { get; private set; } = ByteSize.Zero;

    public ContentHash Hash { get; private set; } = default!;

    public MediaMetadata Metadata { get; private set; } = MediaMetadata.Empty;

    public VirusScanResult ScanResult { get; private set; } = VirusScanResult.NotScanned;

    /// <summary>Accessible description. Filled by the uploader or by an auto-tagger.</summary>
    public string? AltText { get; private set; }

    /// <summary>The client's <c>ImageAttachment.caption</c>.</summary>
    public string? Caption { get; private set; }

    /// <summary>
    /// Why processing failed, when it did. Kept on the asset rather than only on the job so a
    /// member asking "why is my photo broken" can be answered without a join.
    /// </summary>
    public string? FailureReason { get; private set; }

    public IReadOnlyList<MediaVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyList<MediaGrant> Grants => _grants.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private MediaAsset() { }

    private MediaAsset(
        MediaId id,
        UserId uploadedBy,
        MediaKind kind,
        StorageLocation location,
        MediaContentType contentType,
        SafeFileName fileName,
        ByteSize size,
        ContentHash hash) : base(id)
    {
        UploadedBy = uploadedBy;
        Kind = kind;
        Location = location;
        ContentType = contentType;
        FileName = fileName;
        Size = size;
        Hash = hash;

        // Private and pending, always. Widening is an act; it is never a default.
        Status = MediaAssetStatus.Pending;
        Visibility = MediaLimits.DefaultVisibilityFor(kind);
    }

    /// <summary>
    /// Records an asset whose bytes are already in storage.
    /// <para>
    /// Bytes first, record second, deliberately. The reverse order leaves a row pointing at an
    /// object that does not exist whenever the write fails — a dangling reference no reader can
    /// distinguish from a transient outage. This way a failure leaves an orphaned object, which
    /// the sweeper collects.
    /// </para>
    /// </summary>
    public static Result<MediaAsset> Record(
        MediaId id,
        UserId uploadedBy,
        MediaKind kind,
        StorageLocation location,
        MediaContentType contentType,
        SafeFileName fileName,
        ByteSize size,
        ContentHash hash)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(uploadedBy);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(hash);

        if (size.Bytes == 0)
        {
            return Result.Failure<MediaAsset>(MediaErrors.UploadEmpty);
        }

        var ceiling = MediaLimits.MaxSizeFor(kind);

        if (size.Exceeds(ceiling))
        {
            return Result.Failure<MediaAsset>(MediaErrors.TooLarge(size, ceiling));
        }

        var asset = new MediaAsset(
            id, uploadedBy, kind, location, contentType, fileName, size, hash);

        asset.RaiseDomainEvent(new MediaAssetCreatedDomainEvent
        {
            MediaId = id,
            UploadedBy = uploadedBy,
            Kind = kind,
            SizeInBytes = size.Bytes,
            ContentType = contentType.Value
        });

        return Result.Success(asset);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>Moves into processing. Idempotent; a re-entered job does not reset anything.</summary>
    public void BeginProcessing()
    {
        if (Status is MediaAssetStatus.Pending)
        {
            Status = MediaAssetStatus.Processing;
        }
    }

    /// <summary>
    /// Publishes the asset.
    /// <para>
    /// The one guarded transition in the module. It refuses unless the scan says the bytes are
    /// safe to serve — which, by <see cref="VirusScanResult.IsSafeToServe"/>, is false for a
    /// scan that errored as well as for one that found something. A caller cannot talk its way
    /// past this, and there is no other route to <see cref="MediaAssetStatus.Ready"/>.
    /// </para>
    /// </summary>
    public Result MarkReady()
    {
        if (Status is MediaAssetStatus.Quarantined)
        {
            return Result.Failure(MediaErrors.AssetQuarantined);
        }

        if (Status is MediaAssetStatus.Deleted)
        {
            return Result.Failure(MediaErrors.AssetDeleted);
        }

        if (!ScanResult.IsSafeToServe)
        {
            return Result.Failure(MediaErrors.AssetNotReady);
        }

        if (Status is MediaAssetStatus.Ready)
        {
            return Result.Success();
        }

        Status = MediaAssetStatus.Ready;
        FailureReason = null;

        RaiseDomainEvent(new MediaAssetReadyDomainEvent
        {
            MediaId = Id,
            Kind = Kind,
            VariantCount = _variants.Count
        });

        return Result.Success();
    }

    /// <summary>Records a terminal processing failure. The asset stays unreadable.</summary>
    public void MarkFailed(string reason)
    {
        if (Status is MediaAssetStatus.Quarantined or MediaAssetStatus.Deleted)
        {
            return;
        }

        Status = MediaAssetStatus.Failed;
        FailureReason = Truncate(reason, MaxCaptionLength);
    }

    /// <summary>
    /// Records a scan verdict, quarantining the asset when it came back dirty.
    /// <para>
    /// Quarantine is one-way here. Only <see cref="ReleaseFromQuarantine"/>, which is staff-
    /// gated at the application layer, undoes it — a second scan returning clean does not, on
    /// the principle that the interesting case is a scanner whose signatures changed between
    /// two runs and the decision about which run to believe is a human one.
    /// </para>
    /// </summary>
    public void RecordScan(VirusScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (Status is MediaAssetStatus.Deleted)
        {
            return;
        }

        ScanResult = result;

        if (!result.RequiresQuarantine)
        {
            return;
        }

        Status = MediaAssetStatus.Quarantined;
        Visibility = MediaVisibility.Private;

        RaiseDomainEvent(new MediaAssetQuarantinedDomainEvent
        {
            MediaId = Id,
            UploadedBy = UploadedBy,
            Scanner = result.Scanner ?? "unknown",
            ThreatName = result.ThreatName
        });
    }

    /// <summary>
    /// Staff release from quarantine: a scanner's false positive, or a moderation withhold lifted by
    /// a restore on its case.
    /// <para>
    /// <b>Only a scanner's verdict is overruled.</b> When the scanner put the file here, the release
    /// is a person's call that it was a false positive, recorded as a clean result under their name.
    /// When a moderator withheld it, the scan verdict was never the question, and it is left as it
    /// was (7 Nov 2026: a release wrote "clean" over a file that had never been scanned). A file
    /// that has not been scanned goes back to processing to be scanned, not straight to members.
    /// </para>
    /// </summary>
    public Result ReleaseFromQuarantine(DateTimeOffset at, string releasedBy)
    {
        if (Status is not MediaAssetStatus.Quarantined)
        {
            return Result.Success();
        }

        if (ScanResult.RequiresQuarantine)
        {
            ScanResult = VirusScanResult.Clean(releasedBy, at);
        }

        Status = MediaAssetStatus.Processing;

        return Result.Success();
    }

    /// <summary>
    /// Staff take-down: withholds the file from everyone, on a moderation decision.
    /// <para>
    /// <b>The quarantine floor, reached by a person instead of a scanner.</b> The bytes are served
    /// to nobody — the guard's state check runs before every credential, signed links included —
    /// and the file joins the quarantine queue. A restore on its moderation case is how it comes
    /// back. The scan verdict is left alone, because it is a fact about malware and this is not a
    /// finding about malware; visibility is left alone, so a restore puts the file back as it was.
    /// </para>
    /// <para>
    /// <b>Both a hide and a removal end here (7 Nov 2026).</b> A decision never deletes a file: a
    /// deletion starts the byte sweeper, and a removal has to be reversible on appeal.
    /// </para>
    /// <para>
    /// <b>The reason is the record.</b> It is kept in <see cref="FailureReason"/>, the field this
    /// aggregate already uses to answer a member asking why their file does not show, and the
    /// quarantine queue reads it back to staff.
    /// </para>
    /// </summary>
    public Result Withhold(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(MediaErrors.ModerationReasonRequired);
        }

        if (Status is MediaAssetStatus.Deleted)
        {
            return Result.Failure(MediaErrors.AssetDeleted);
        }

        Status = MediaAssetStatus.Quarantined;
        FailureReason = Truncate(reason, MaxCaptionLength);

        return Result.Success();
    }

    public Result Delete(UserId deletedBy)
    {
        ArgumentNullException.ThrowIfNull(deletedBy);

        if (Status is MediaAssetStatus.Deleted)
        {
            return Result.Success();
        }

        Status = MediaAssetStatus.Deleted;

        RaiseDomainEvent(new MediaAssetDeletedDomainEvent
        {
            MediaId = Id,
            DeletedBy = deletedBy
        });

        return Result.Success();
    }

    // ── Description and ownership ────────────────────────────────────────

    public void Describe(string? altText, string? caption)
    {
        AltText = Truncate(altText, MaxAltTextLength);
        Caption = Truncate(caption, MaxCaptionLength);
    }

    /// <summary>
    /// Claims the asset for an owning entity.
    /// <para>
    /// Re-attaching to a different owner is refused. An asset that could be moved between
    /// owners is an asset whose access rules change under the module that was relying on
    /// them — the housing listing's photo silently becoming a marketplace listing's photo,
    /// with a different audience.
    /// </para>
    /// </summary>
    public Result AttachTo(MediaOwner owner, string role)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!owner.IsAssigned)
        {
            return Result.Failure(MediaErrors.ReferenceOwnerInvalid);
        }

        if (Owner.IsAssigned && !Owner.Equals(owner))
        {
            return Result.Failure(MediaErrors.OwnershipRequired);
        }

        var reference = MediaReference.Create(Id, owner, role);

        Owner = owner;
        Role = reference.Role;

        RaiseDomainEvent(new MediaAssetAttachedDomainEvent
        {
            MediaId = Id,
            OwnerType = owner.Type,
            OwnerId = owner.Id,
            Role = reference.Role
        });

        return Result.Success();
    }

    /// <summary>Changes who may read the asset.</summary>
    public Result ChangeVisibility(MediaVisibility visibility)
    {
        if (visibility == MediaVisibility.Public && !MediaLimits.CanBePublic(Kind))
        {
            return Result.Failure(MediaErrors.SignedUrlNotPermitted);
        }

        if (Status is MediaAssetStatus.Quarantined)
        {
            return Result.Failure(MediaErrors.AssetQuarantined);
        }

        Visibility = visibility;
        return Result.Success();
    }

    // ── Metadata ─────────────────────────────────────────────────────────

    public Result ApplyMetadata(MediaMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.Dimensions is { } dimensions &&
            (dimensions.Width > MediaDimensions.MaxEdge || dimensions.Height > MediaDimensions.MaxEdge))
        {
            return Result.Failure(MediaErrors.DimensionsExceeded(dimensions));
        }

        Metadata = metadata;
        return Result.Success();
    }

    // ── Variants ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a derived rendition.
    /// <para>
    /// One variant per purpose, replacing rather than duplicating. Re-running the pipeline
    /// after a codec upgrade should improve the thumbnail, not leave two of them with no rule
    /// for which is served.
    /// </para>
    /// </summary>
    public Result<MediaVariant> AddVariant(
        MediaVariantPurpose purpose,
        StorageLocation location,
        MediaContentType contentType,
        ByteSize size,
        MediaDimensions? dimensions,
        string? transformationKey)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(size);

        if (purpose == MediaVariantPurpose.Original)
        {
            return Result.Failure<MediaVariant>(MediaErrors.VariantNotSupported);
        }

        var existing = _variants.FirstOrDefault(variant => variant.Purpose == purpose);

        if (existing is not null)
        {
            _variants.Remove(existing);
        }
        else if (_variants.Count >= MaxVariants)
        {
            return Result.Failure<MediaVariant>(MediaErrors.VariantNotSupported);
        }

        var variant = MediaVariant.Create(
            Id, purpose, location, contentType, size, dimensions, transformationKey);

        _variants.Add(variant);

        RaiseDomainEvent(new MediaVariantAddedDomainEvent
        {
            MediaId = Id,
            Purpose = purpose,
            SizeInBytes = size.Bytes
        });

        return Result.Success(variant);
    }

    /// <summary>
    /// The best rendition for a requested purpose.
    /// <para>
    /// Falls back to the original rather than failing. A client asking for a thumbnail on a
    /// deployment with no image codec should get a picture — the original, larger than it
    /// wanted — rather than a broken image. Degrading in quality beats degrading in function.
    /// </para>
    /// </summary>
    public MediaVariant? FindVariant(MediaVariantPurpose purpose) =>
        _variants.FirstOrDefault(variant => variant.Purpose == purpose);

    public bool HasVariant(MediaVariantPurpose purpose) => FindVariant(purpose) is not null;

    // ── Access ───────────────────────────────────────────────────────────

    /// <summary>
    /// Grants a member explicit access, optionally until a moment.
    /// <para>
    /// Used where visibility is too coarse: a verification document is private, and a reviewer
    /// needs to read exactly that one, for exactly as long as the review takes.
    /// </para>
    /// </summary>
    public Result<MediaGrant> Grant(
        UserId grantee, MediaAccessLevel level, DateTimeOffset? expiresAt, UserId grantedBy)
    {
        ArgumentNullException.ThrowIfNull(grantee);
        ArgumentNullException.ThrowIfNull(grantedBy);

        var existing = _grants.FirstOrDefault(grant =>
            grant.GranteeId == grantee && !grant.IsRevoked);

        if (existing is not null)
        {
            existing.Amend(level, expiresAt);
            return Result.Success(existing);
        }

        var granted = MediaGrant.Create(Id, grantee, level, expiresAt, grantedBy);
        _grants.Add(granted);

        return Result.Success(granted);
    }

    public Result Revoke(UserId grantee)
    {
        ArgumentNullException.ThrowIfNull(grantee);

        foreach (var grant in _grants.Where(grant => grant.GranteeId == grantee))
        {
            grant.Revoke();
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether a caller may read this asset, from the asset's own knowledge alone.
    /// <para>
    /// Deliberately not the whole answer. This decides ownership, explicit grants, visibility
    /// and status — the parts Media owns. Whether a member may see the photo attached to a
    /// private housing listing is Housing's judgement, asked through
    /// <c>IMediaAccessPolicy</c>, because Media does not know what a private listing is and
    /// should never learn.
    /// </para>
    /// </summary>
    public bool CanBeReadBy(UserId? caller, DateTimeOffset asOf)
    {
        if (Status is MediaAssetStatus.Deleted or MediaAssetStatus.Quarantined)
        {
            return false;
        }

        if (caller is not null && UploadedBy == caller)
        {
            // The uploader sees their own file while it is still being processed. Everyone
            // else waits for Ready — which is what makes an upload feel instant to the person
            // who made it without exposing an unscanned file to anyone else.
            return true;
        }

        if (Status is not MediaAssetStatus.Ready)
        {
            return false;
        }

        if (Visibility is MediaVisibility.Public)
        {
            return true;
        }

        if (caller is null)
        {
            return false;
        }

        if (Visibility is MediaVisibility.Authenticated)
        {
            return true;
        }

        return _grants.Any(grant => grant.PermitsRead(caller, asOf));
    }

    /// <summary>Whether a caller may change or delete this asset.</summary>
    public bool CanBeManagedBy(UserId? caller, DateTimeOffset asOf)
    {
        if (caller is null || Status is MediaAssetStatus.Deleted)
        {
            return false;
        }

        return UploadedBy == caller ||
               _grants.Any(grant => grant.Permits(caller, MediaAccessLevel.Manage, asOf));
    }

    /// <summary>
    /// Whether the asset must be served through a short-lived signed link rather than a
    /// cacheable one. Anything not public, and every verification document regardless.
    /// </summary>
    public bool RequiresSignedAccess =>
        Visibility is not MediaVisibility.Public || Kind is MediaKind.VerificationDocument;

    /// <summary>How long a link to this asset may live.</summary>
    public TimeSpan SignedUrlLifetime =>
        Kind is MediaKind.VerificationDocument
            ? MediaLimits.SensitiveSignedUrlLifetime
            : MediaLimits.SignedUrlLifetime;

    // ── Auditing ─────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        Status = MediaAssetStatus.Deleted;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = MediaAssetStatus.Processing;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
