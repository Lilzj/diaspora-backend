namespace Diaspora.Modules.Media;

/// <summary>
/// Stable, client-facing error codes for the module's problem documents.
/// <para>
/// Frozen alongside the API: a client branching on <c>media.upload.too-large</c> to show
/// "that file is over the limit" must keep working across releases, so these strings change
/// only with a new API version.
/// </para>
/// <para>
/// The upload codes are deliberately specific. "Bad request" tells a member nothing;
/// <c>media.upload.type-not-allowed</c> versus <c>media.upload.too-large</c> versus
/// <c>media.upload.signature-mismatch</c> are three different sentences in the UI and three
/// different support outcomes.
/// </para>
/// </summary>
public static class MediaErrorCodes
{
    public const string NotAuthenticated = "media.not-authenticated";

    // Assets
    public const string AssetNotFound = "media.asset.not-found";
    public const string AssetNotReady = "media.asset.not-ready";
    public const string AssetQuarantined = "media.asset.quarantined";
    public const string AssetDeleted = "media.asset.deleted";
    public const string AssetAccessDenied = "media.asset.access-denied";
    public const string AssetOwnershipRequired = "media.asset.ownership-required";
    public const string ProfileImageNotAnImage = "media.profile-image.not-an-image";

    // Moderation
    public const string ModerationReasonRequired = "media.moderation.reason-required";
    public const string ModerationActionUnknown = "media.moderation.action-unknown";
    public const string WithheldByModeration = "media.moderation.withheld";

    // Upload lifecycle
    public const string UploadSessionNotFound = "media.upload.not-found";
    public const string UploadSessionExpired = "media.upload.expired";
    public const string UploadSessionNotOpen = "media.upload.not-open";
    public const string UploadSessionIncomplete = "media.upload.incomplete";
    public const string UploadChunkOutOfRange = "media.upload.chunk-out-of-range";
    public const string UploadChunkSizeInvalid = "media.upload.chunk-size-invalid";
    public const string UploadChunkChecksumMismatch = "media.upload.chunk-checksum-mismatch";
    public const string UploadTooLarge = "media.upload.too-large";
    public const string UploadEmpty = "media.upload.empty";
    public const string UploadSizeMismatch = "media.upload.size-mismatch";
    public const string UploadQuotaExceeded = "media.upload.quota-exceeded";

    // Validation
    public const string ContentTypeNotAllowed = "media.upload.type-not-allowed";
    public const string ExtensionNotAllowed = "media.upload.extension-not-allowed";
    public const string SignatureMismatch = "media.upload.signature-mismatch";
    public const string FileNameInvalid = "media.upload.filename-invalid";
    public const string DimensionsExceeded = "media.upload.dimensions-exceeded";
    public const string MalwareDetected = "media.upload.malware-detected";

    // Storage
    public const string StorageUnavailable = "media.storage.unavailable";
    public const string StorageProviderUnknown = "media.storage.provider-unknown";
    public const string StorageObjectMissing = "media.storage.object-missing";

    // Variants and processing
    public const string VariantNotFound = "media.variant.not-found";
    public const string VariantNotSupported = "media.variant.not-supported";
    public const string TransformationInvalid = "media.transformation.invalid";
    public const string TransformationUnavailable = "media.transformation.unavailable";
    public const string ProcessingJobNotFound = "media.processing.not-found";
    public const string ProcessingFailed = "media.processing.failed";

    // Collections and references
    public const string CollectionNotFound = "media.collection.not-found";
    public const string CollectionFull = "media.collection.full";
    public const string CollectionItemNotFound = "media.collection.item-not-found";
    public const string CollectionKindMismatch = "media.collection.kind-mismatch";
    public const string ReferenceOwnerInvalid = "media.reference.owner-invalid";

    // Signed access
    public const string SignedUrlInvalid = "media.signed-url.invalid";
    public const string SignedUrlExpired = "media.signed-url.expired";
    public const string SignedUrlNotPermitted = "media.signed-url.not-permitted";
}
