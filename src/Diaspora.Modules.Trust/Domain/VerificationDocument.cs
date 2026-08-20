using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One piece of evidence attached to a verification request.
/// <para>
/// An entity inside the <see cref="VerificationRequest"/> aggregate, never loaded or
/// modified on its own — every state change goes through the request, which is what keeps
/// "a rejected document must carry a reason" enforceable in one place.
/// </para>
/// <para>
/// The module stores a storage <em>key</em>, not a URL. Issuing a readable link is the
/// storage abstraction's job (<c>IStorageService.GetPresignedUrlAsync</c>) and happens at
/// the read boundary, so an immigration document never sits in the database as a
/// permanently-live link. The frontend calls this field <c>documentUrl</c> in one type and
/// <c>documentUri</c> in another, and neither holds a URL today.
/// </para>
/// </summary>
public sealed class VerificationDocument : Entity<VerificationDocumentId>
{
    public const int MaxStorageKeyLength = 512;
    public const int MaxOriginalFileNameLength = 260;
    public const int MaxContentTypeLength = 128;
    public const int MaxReviewCommentLength = 1000;
    public const int MaxDocumentNumberLength = 64;

    /// <summary>Largest accepted upload. The wizard advertises 5 MB; the server enforces it.</summary>
    public const long MaxSizeInBytes = 5 * 1024 * 1024;

    public VerificationRequestId RequestId { get; private set; } = default!;

    public VerificationDocumentType DocumentType { get; private set; }

    public VerificationDocumentStatus Status { get; private set; }

    /// <summary>The <c>IStorageService</c> key. Never rendered to a client directly.</summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>
    /// The Media asset this document is, when the evidence arrived as a Media reference rather
    /// than a direct upload.
    /// <para>
    /// <b>A reference, never a URL</b> — ADR-005's rule, and the reason a verification cannot
    /// leak a document: the readable link is minted by Media at read time, against the reader,
    /// and Trust never holds one. Null for a document uploaded through the member's own wizard,
    /// which supplies a storage key directly.
    /// </para>
    /// <para>
    /// When this is set, <see cref="StorageKey"/> carries the same identifier under a
    /// <c>media:</c> prefix. Namespaced deliberately: a bare GUID sitting in a column that
    /// elsewhere holds object-store paths is the kind of ambiguity that eventually gets passed
    /// to a storage client, and the prefix makes that impossible to do by accident.
    /// </para>
    /// </summary>
    public Guid? MediaId { get; private set; }

    public string? OriginalFileName { get; private set; }

    public string? ContentType { get; private set; }

    public long SizeInBytes { get; private set; }

    /// <summary>
    /// The document's own identifier — passport number, permit number.
    /// <para>
    /// The diaspora wizard collects this and then discards it: it is validated at
    /// <c>diaspora.tsx:169</c> and displayed at <c>:565</c>, but absent from
    /// <c>SubmitVerificationRequest</c>. Captured here because a reviewer cannot check a
    /// document against a registry without it.
    /// </para>
    /// </summary>
    public string? DocumentNumber { get; private set; }

    /// <summary>Where the document was issued. Drives the nationality cross-check.</summary>
    public string? IssuingCountry { get; private set; }

    public DateOnly? IssuedOn { get; private set; }

    public DateOnly? ExpiresOn { get; private set; }

    /// <summary>
    /// Which submission this is. Re-uploading the same document type supersedes the previous
    /// one rather than deleting it — a reviewer needs to see what was originally sent.
    /// </summary>
    public int Version { get; private set; }

    public string? ReviewComment { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    private VerificationDocument() { }

    private VerificationDocument(
        VerificationDocumentId id,
        VerificationRequestId requestId,
        VerificationDocumentType documentType,
        string storageKey,
        int version,
        DateTimeOffset uploadedAt) : base(id)
    {
        RequestId = requestId;
        DocumentType = documentType;
        StorageKey = storageKey;
        Status = VerificationDocumentStatus.Pending;
        Version = version;
        UploadedAt = uploadedAt;
    }

    internal static VerificationDocument Attach(
        VerificationRequestId requestId,
        VerificationDocumentType documentType,
        string storageKey,
        int version,
        DateTimeOffset uploadedAt)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        var key = Guard.Against.NullOrWhiteSpace(storageKey, nameof(storageKey)).Trim();
        Guard.Against.MaxLength(key, MaxStorageKeyLength, nameof(storageKey));

        return new VerificationDocument(
            VerificationDocumentId.New(), requestId, documentType, key, version, uploadedAt);
    }

    /// <summary>
    /// Records that this document is a Media asset.
    /// <para>
    /// The identifier only. Trust does not copy the bytes, the name, the size or the content
    /// type from Media, and it does not claim ownership of the asset — duplicating any of that
    /// would give the platform two answers to "what is this file" and one of them would go
    /// stale. Media stays the owner; this is a pointer.
    /// </para>
    /// </summary>
    internal VerificationDocument WithMediaReference(Guid mediaId)
    {
        MediaId = mediaId;
        return this;
    }

    /// <summary>The storage-key form of a Media reference. Namespaced so it can never be mistaken
    /// for an object-store path.</summary>
    internal static string MediaStorageKey(Guid mediaId) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"media:{mediaId:D}");

    internal VerificationDocument WithFileMetadata(string? fileName, string? contentType, long sizeInBytes)
    {
        OriginalFileName = Truncate(fileName, MaxOriginalFileNameLength);
        ContentType = Truncate(contentType, MaxContentTypeLength);
        SizeInBytes = sizeInBytes < 0 ? 0 : sizeInBytes;
        return this;
    }

    internal VerificationDocument WithDocumentDetails(
        string? documentNumber,
        string? issuingCountry,
        DateOnly? issuedOn,
        DateOnly? expiresOn)
    {
        DocumentNumber = Truncate(documentNumber, MaxDocumentNumberLength);
        IssuingCountry = NormalizeCountry(issuingCountry);
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        return this;
    }

    /// <summary>
    /// Whether the document has lapsed as at <paramref name="asOf"/>. A document with no
    /// stated expiry never expires — a passport stamp has no expiry of its own.
    /// </summary>
    public bool IsExpired(DateOnly asOf) => ExpiresOn is { } expiry && expiry < asOf;

    internal void Accept(string? comment, DateTimeOffset reviewedAt)
    {
        Status = VerificationDocumentStatus.Accepted;
        ReviewComment = Truncate(comment, MaxReviewCommentLength);
        ReviewedAt = reviewedAt;
    }

    internal void Reject(string comment, DateTimeOffset reviewedAt)
    {
        Status = VerificationDocumentStatus.Rejected;
        ReviewComment = Truncate(
            Guard.Against.NullOrWhiteSpace(comment, nameof(comment)), MaxReviewCommentLength);
        ReviewedAt = reviewedAt;
    }

    internal void Supersede() => Status = VerificationDocumentStatus.Superseded;

    internal void MarkExpired() => Status = VerificationDocumentStatus.Expired;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
