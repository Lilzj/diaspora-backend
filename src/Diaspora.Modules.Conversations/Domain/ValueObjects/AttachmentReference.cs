using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// A file on a message — as an identifier and its shape, never as bytes and never as a URL.
/// <para>
/// <b>This module stores no files and issues no URLs, and both halves of that matter.</b> Media
/// (B5) owns the asset, its storage tier, its virus scan, its thumbnails and its access policy; a
/// conversation that held a URL would be holding a credential with an expiry it does not control,
/// and the first time B5 rotated its signing key every photograph ever sent would 403. What is held
/// here is the asset identifier and the dimensions the client needs to lay out a bubble before the
/// image loads — width, height, duration, size — because those are properties of the message, not of
/// the storage.
/// </para>
/// <para>
/// <b><see cref="AttachmentId"/> is supplied by the client, not generated here.</b> The client mints
/// it when the user picks a file and renders an upload progress bar against it, long before the
/// message exists. Generating a second identifier server-side would leave the client unable to match
/// the confirmed message to the bar it is already showing.
/// </para>
/// </summary>
public sealed class AttachmentReference : ValueObject
{
    public const int MaxFileNameLength = 200;
    public const int MaxMimeTypeLength = 128;
    public const int MaxCaptionLength = 500;

    private AttachmentReference(
        ConversationAttachmentId attachmentId,
        AttachmentKind kind,
        Guid mediaAssetId,
        Guid? thumbnailMediaId,
        string? mimeType,
        long? sizeBytes,
        int? width,
        int? height,
        int? durationSeconds,
        string? fileName,
        int? pageCount,
        string? caption)
    {
        AttachmentId = attachmentId;
        Kind = kind;
        MediaAssetId = mediaAssetId;
        ThumbnailMediaId = thumbnailMediaId;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        Width = width;
        Height = height;
        DurationSeconds = durationSeconds;
        FileName = fileName;
        PageCount = pageCount;
        Caption = caption;
    }

    private AttachmentReference() { }

    public ConversationAttachmentId AttachmentId { get; private set; } = default!;

    public AttachmentKind Kind { get; private set; }

    /// <summary>The B5 asset. The only thing that can produce bytes.</summary>
    public Guid MediaAssetId { get; private set; }

    /// <summary>A second B5 asset, when Media produced a derivative worth addressing separately.</summary>
    public Guid? ThumbnailMediaId { get; private set; }

    public string? MimeType { get; private set; }

    public long? SizeBytes { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public int? DurationSeconds { get; private set; }

    public string? FileName { get; private set; }

    public int? PageCount { get; private set; }

    public string? Caption { get; private set; }

    public static AttachmentReference Create(
        ConversationAttachmentId attachmentId,
        AttachmentKind kind,
        Guid mediaAssetId,
        Guid? thumbnailMediaId = null,
        string? mimeType = null,
        long? sizeBytes = null,
        int? width = null,
        int? height = null,
        int? durationSeconds = null,
        string? fileName = null,
        int? pageCount = null,
        string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(attachmentId);

        if (mediaAssetId == Guid.Empty)
        {
            throw new ArgumentException(
                "An attachment must name a Media asset. A conversation cannot store the file itself.",
                nameof(mediaAssetId));
        }

        return new AttachmentReference(
            attachmentId,
            kind,
            mediaAssetId,
            thumbnailMediaId == Guid.Empty ? null : thumbnailMediaId,
            Clamp(mimeType, MaxMimeTypeLength),
            Positive(sizeBytes),
            Positive(width),
            Positive(height),
            Positive(durationSeconds),
            Clamp(fileName, MaxFileNameLength),
            Positive(pageCount),
            Clamp(caption, MaxCaptionLength));
    }

    /// <summary>The message kind an attachment of this shape implies, when the caller named none.</summary>
    public MessageKind ImpliedMessageKind() => Kind switch
    {
        AttachmentKind.Image => MessageKind.Image,
        AttachmentKind.Voice => MessageKind.Voice,
        AttachmentKind.Video => MessageKind.Video,
        AttachmentKind.Location => MessageKind.Location,
        _ => MessageKind.Document
    };

    private static string? Clamp(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static int? Positive(int? value) => value is > 0 ? value : null;

    private static long? Positive(long? value) => value is > 0 ? value : null;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttachmentId;
        yield return Kind;
        yield return MediaAssetId;
        yield return ThumbnailMediaId;
        yield return MimeType;
        yield return SizeBytes;
        yield return Width;
        yield return Height;
        yield return DurationSeconds;
        yield return FileName;
        yield return PageCount;
        yield return Caption;
    }
}
