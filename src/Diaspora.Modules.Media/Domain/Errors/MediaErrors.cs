using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.Errors;

/// <summary>
/// Every failure the Media domain returns, in one place.
/// <para>
/// Declared once rather than constructed at each call site so that two handlers rejecting the
/// same condition produce the same code and the same sentence. A client branching on
/// <c>media.upload.too-large</c> to render "that file is too big" cannot do so if half the
/// paths return a generic validation error instead.
/// </para>
/// <para>
/// The messages are written to be read by a member, not by an engineer. "That file is 18.4 MB;
/// images may be up to 15 MB" is a sentence someone can act on.
/// </para>
/// </summary>
public static class MediaErrors
{
    // ── Identity ─────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        MediaErrorCodes.NotAuthenticated, "You must be signed in to use media.");

    // ── Assets ───────────────────────────────────────────────────────────

    public static readonly Error AssetNotFound = Error.NotFound(
        MediaErrorCodes.AssetNotFound, "That file does not exist.");

    public static readonly Error AssetNotReady = Error.Conflict(
        MediaErrorCodes.AssetNotReady, "That file is still being processed.");

    public static readonly Error AssetQuarantined = Error.Forbidden(
        MediaErrorCodes.AssetQuarantined,
        "That file was flagged during a security scan and cannot be opened.");

    public static readonly Error AssetDeleted = Error.NotFound(
        MediaErrorCodes.AssetDeleted, "That file has been deleted.");

    public static readonly Error AssetAccessDenied = Error.Forbidden(
        MediaErrorCodes.AssetAccessDenied, "You do not have access to that file.");

    public static readonly Error OwnershipRequired = Error.Forbidden(
        MediaErrorCodes.AssetOwnershipRequired, "Only the owner of a file can do that.");

    // ── Upload sessions ──────────────────────────────────────────────────

    public static readonly Error UploadSessionNotFound = Error.NotFound(
        MediaErrorCodes.UploadSessionNotFound, "That upload could not be found.");

    public static readonly Error UploadSessionExpired = Error.Conflict(
        MediaErrorCodes.UploadSessionExpired,
        "That upload expired before it finished. Start it again.");

    public static readonly Error UploadSessionNotOpen = Error.Conflict(
        MediaErrorCodes.UploadSessionNotOpen, "That upload is no longer accepting data.");

    public static readonly Error UploadIncomplete = Error.Conflict(
        MediaErrorCodes.UploadSessionIncomplete,
        "Some parts of the file have not arrived yet.");

    public static readonly Error UploadEmpty = Error.Validation(
        MediaErrorCodes.UploadEmpty, "The file is empty.");

    public static readonly Error ChunkOutOfRange = Error.Validation(
        MediaErrorCodes.UploadChunkOutOfRange, "That part number is not part of this upload.");

    public static readonly Error ChunkChecksumMismatch = Error.Validation(
        MediaErrorCodes.UploadChunkChecksumMismatch,
        "That part did not arrive intact. Send it again.");

    public static readonly Error UploadSizeMismatch = Error.Validation(
        MediaErrorCodes.UploadSizeMismatch,
        "The assembled file is not the size the upload declared.");

    public static readonly Error UploadQuotaExceeded = Error.RateLimited(
        MediaErrorCodes.UploadQuotaExceeded,
        "You have too many uploads in progress. Finish or cancel one first.");

    public static Error ChunkTooSmall(ByteSize minimum) => Error.Validation(
        MediaErrorCodes.UploadChunkSizeInvalid,
        $"Every part except the last must be at least {minimum}.");

    public static Error TooLarge(ByteSize actual, ByteSize limit) => Error.Validation(
        MediaErrorCodes.UploadTooLarge,
        $"That file is {actual}; the limit for this kind of file is {limit}.");

    // ── Validation ───────────────────────────────────────────────────────

    public static Error ContentTypeNotAllowed(MediaContentType contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        return Error.Validation(
            MediaErrorCodes.ContentTypeNotAllowed,
            $"Files of type {contentType.Value} are not accepted here.");
    }

    public static Error ExtensionNotAllowed(FileExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return Error.Validation(
            MediaErrorCodes.ExtensionNotAllowed,
            $".{extension.Value} files are not accepted here.");
    }

    /// <summary>
    /// The bytes are not what the upload claimed. Phrased without naming what they actually
    /// are: telling a prober that their disguised archive was recognised as an archive is a
    /// free oracle.
    /// </summary>
    public static readonly Error SignatureMismatch = Error.Validation(
        MediaErrorCodes.SignatureMismatch,
        "That file's contents do not match the type it was uploaded as.");

    public static readonly Error FileNameInvalid = Error.Validation(
        MediaErrorCodes.FileNameInvalid, "That file name cannot be used.");

    public static readonly Error MalwareDetected = Error.Validation(
        MediaErrorCodes.MalwareDetected,
        "That file was flagged by a security scan and was not stored.");

    public static Error DimensionsExceeded(MediaDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);

        return Error.Validation(
            MediaErrorCodes.DimensionsExceeded,
            $"That image is {dimensions}, which is larger than the platform accepts.");
    }

    // ── Storage ──────────────────────────────────────────────────────────

    public static readonly Error StorageUnavailable = Error.ServiceUnavailable(
        MediaErrorCodes.StorageUnavailable, "File storage is temporarily unavailable.");

    public static readonly Error StorageObjectMissing = Error.NotFound(
        MediaErrorCodes.StorageObjectMissing, "The stored file is missing.");

    public static Error StorageProviderUnknown(string provider) => Error.ServiceUnavailable(
        MediaErrorCodes.StorageProviderUnknown,
        $"No storage provider named '{provider}' is configured in this deployment.");

    // ── Variants and processing ──────────────────────────────────────────

    public static readonly Error VariantNotFound = Error.NotFound(
        MediaErrorCodes.VariantNotFound, "That version of the file does not exist.");

    public static readonly Error VariantNotSupported = Error.Validation(
        MediaErrorCodes.VariantNotSupported, "That version cannot be produced for this file.");

    public static readonly Error TransformationUnavailable = Error.ServiceUnavailable(
        MediaErrorCodes.TransformationUnavailable,
        "No processor in this deployment can produce that version.");

    public static readonly Error ProcessingJobNotFound = Error.NotFound(
        MediaErrorCodes.ProcessingJobNotFound, "That processing job does not exist.");

    // ── Collections ──────────────────────────────────────────────────────

    public static readonly Error CollectionNotFound = Error.NotFound(
        MediaErrorCodes.CollectionNotFound, "That collection does not exist.");

    public static readonly Error CollectionItemNotFound = Error.NotFound(
        MediaErrorCodes.CollectionItemNotFound, "That file is not in this collection.");

    public static readonly Error CollectionKindMismatch = Error.Validation(
        MediaErrorCodes.CollectionKindMismatch,
        "That kind of file cannot go in this collection.");

    public static Error CollectionFull(int limit) => Error.Validation(
        MediaErrorCodes.CollectionFull,
        $"This collection holds at most {limit} files.");

    public static readonly Error ReferenceOwnerInvalid = Error.Validation(
        MediaErrorCodes.ReferenceOwnerInvalid, "That is not a valid owner for a file.");

    // ── Signed access ────────────────────────────────────────────────────

    public static readonly Error SignedUrlInvalid = Error.Forbidden(
        MediaErrorCodes.SignedUrlInvalid, "That link is not valid.");

    public static readonly Error SignedUrlExpired = Error.Forbidden(
        MediaErrorCodes.SignedUrlExpired, "That link has expired.");

    public static readonly Error SignedUrlNotPermitted = Error.Forbidden(
        MediaErrorCodes.SignedUrlNotPermitted,
        "Signed links cannot be issued for that file.");
}
