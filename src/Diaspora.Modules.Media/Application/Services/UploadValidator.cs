using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Decides whether bytes may be accepted, from the bytes themselves.
/// <para>
/// <b>The client validates nothing.</b> Not one screen in the app checks a file's size,
/// extension, type or dimensions before accepting it — the 5 MB limit is rendered as helper
/// text and never applied, and <c>quality: 0.8</c> is a compression hint to the picker, not a
/// check. Every rule the product advertises is therefore enforced here or it is not enforced.
/// </para>
/// <para>
/// <b>The check that matters most is the signature check.</b> The declared content type came
/// from a header the caller wrote and the extension came from a name the caller chose; only
/// the magic bytes come from the file. Comparing them closes polyglot files, double
/// extensions, and every variant of "rename the payload to .jpg and let the CDN serve it as
/// script" — in one comparison, before a byte is committed.
/// </para>
/// </summary>
public sealed class UploadValidator
{
    private readonly MediaUploadOptions _options;
    private readonly ILogger<UploadValidator> _logger;

    public UploadValidator(IOptions<MediaUploadOptions> options, ILogger<UploadValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Everything decidable before any bytes arrive: type, extension, size.
    /// <para>
    /// Front-loaded deliberately. Refusing a 600 MB video at initialisation costs one round
    /// trip; refusing it after the bytes have crossed a mobile network costs the member their
    /// data allowance either way.
    /// </para>
    /// </summary>
    public static Result ValidateDeclaration(
        MediaKind kind, MediaContentType contentType, SafeFileName fileName, ByteSize size)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(size);

        if (size.Bytes <= 0)
        {
            return Reject(MediaErrors.UploadEmpty, "empty");
        }

        var ceiling = MediaLimits.MaxSizeFor(kind);

        if (size.Exceeds(ceiling))
        {
            return Reject(MediaErrors.TooLarge(size, ceiling), "too-large");
        }

        if (!MediaTypeAllowList.AllowsContentType(kind, contentType))
        {
            return Reject(MediaErrors.ContentTypeNotAllowed(contentType), "type-not-allowed");
        }

        if (!MediaTypeAllowList.AllowsExtension(kind, fileName.Extension))
        {
            return Reject(MediaErrors.ExtensionNotAllowed(fileName.Extension), "extension-not-allowed");
        }

        return Result.Success();
    }

    /// <summary>
    /// Checks the bytes against the declaration.
    /// <para>
    /// Runs on the first chunk, so a disguised file is refused after five megabytes rather than
    /// after five hundred. It needs only the head — the signature table reads at most
    /// <see cref="FileSignature.InspectionLength"/> bytes — which is what makes checking this
    /// early possible at all.
    /// </para>
    /// </summary>
    public Result ValidateSignature(
        MediaKind kind, MediaContentType declared, FileExtension extension, ReadOnlySpan<byte> head)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(extension);

        if (!_options.EnforceSignatureValidation)
        {
            // Off only for diagnosing a false positive against a format the table does not yet
            // know. The module warns at startup when this is off, because it removes the
            // single most valuable control it has.
            return Result.Success();
        }

        var signature = FileSignatureReader.Identify(head);

        if (!signature.IsRecognized)
        {
            _logger.LogInformation(
                "Refused an upload declared as {ContentType}: the leading bytes match no known format.",
                declared.Value);

            return Reject(MediaErrors.SignatureMismatch, "signature-unrecognized");
        }

        if (!MediaTypeAllowList.AllowsSignature(kind, signature, extension))
        {
            _logger.LogWarning(
                "Refused an upload declared as {ContentType} whose contents are {Actual}.",
                declared.Value, signature.Description);

            return Reject(MediaErrors.SignatureMismatch, "signature-mismatch");
        }

        if (!signature.IsConsistentWith(declared) &&
            !IsAcceptableDisagreement(kind, signature, extension))
        {
            return Reject(MediaErrors.SignatureMismatch, "signature-mismatch");
        }

        return Result.Success();
    }

    /// <summary>Checks discovered dimensions against the decompression-bomb ceiling.</summary>
    public static Result ValidateDimensions(MediaDimensions? dimensions)
    {
        if (dimensions is null)
        {
            return Result.Success();
        }

        if (dimensions.Width > MediaDimensions.MaxEdge || dimensions.Height > MediaDimensions.MaxEdge)
        {
            return Reject(MediaErrors.DimensionsExceeded(dimensions), "dimensions");
        }

        return Result.Success();
    }

    /// <summary>
    /// The two places a signature legitimately disagrees with a declared type.
    /// <para>
    /// A <c>.docx</c> is a ZIP archive, so its bytes say archive while its type says document.
    /// And verification evidence is a policy label rather than a format — the same upload is a
    /// PDF or a photograph, and both are correct.
    /// </para>
    /// </summary>
    private static bool IsAcceptableDisagreement(
        MediaKind kind, FileSignature signature, FileExtension extension) =>
        (kind is MediaKind.Document &&
         signature.Kind is MediaKind.Archive &&
         extension.Value is "docx" or "xlsx" or "pptx") ||
        (kind is MediaKind.VerificationDocument &&
         signature.Kind is MediaKind.Document or MediaKind.Image);

    private static Result Reject(Error error, string reason)
    {
        MediaTelemetry.UploadsRejected.Add(1,
            new KeyValuePair<string, object?>("reason", reason));

        return Result.Failure(error);
    }
}
