using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// An RFC 6838 media type — <c>image/jpeg</c>, <c>application/pdf</c>.
/// <para>
/// Named <c>MediaContentType</c> rather than <c>ContentType</c> to stay clear of
/// <see cref="System.Net.Mime.ContentType"/>, which a persistence or HTTP file in this module
/// will import sooner or later.
/// </para>
/// <para>
/// Parsing drops parameters. <c>text/plain; charset=utf-8</c> becomes <c>text/plain</c>,
/// because the parameter is a transport detail and letting it into the stored value means two
/// rows for one format and a lookup that misses half of them.
/// </para>
/// </summary>
public sealed class MediaContentType : ValueObject
{
    public const int MaxLength = 128;

    /// <summary>What a browser gets when nothing better is known. Never renders inline.</summary>
    public static readonly MediaContentType OctetStream = new("application", "octet-stream");

    private MediaContentType(string type, string subtype)
    {
        Type = type;
        Subtype = subtype;
    }

    /// <summary>The top-level type: <c>image</c>, <c>video</c>, <c>application</c>.</summary>
    public string Type { get; }

    /// <summary>The subtype: <c>jpeg</c>, <c>pdf</c>.</summary>
    public string Subtype { get; }

    public string Value => $"{Type}/{Subtype}";

    public bool IsImage => string.Equals(Type, "image", StringComparison.Ordinal);

    public bool IsVideo => string.Equals(Type, "video", StringComparison.Ordinal);

    public bool IsAudio => string.Equals(Type, "audio", StringComparison.Ordinal);

    /// <summary>
    /// The coarse policy axis this type falls under, before any magic-byte check.
    /// <para>
    /// A declared type is a claim, not a fact — which is why the caller resolves the kind
    /// again from the sniffed signature and refuses the upload when the two disagree.
    /// </para>
    /// </summary>
    public MediaKind ToKind() => Type switch
    {
        "image" => MediaKind.Image,
        "video" => MediaKind.Video,
        "audio" => MediaKind.Audio,
        "application" or "text" => ApplicationKind(),
        _ => MediaKind.Unknown
    };

    public static Result<MediaContentType> TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<MediaContentType>(MediaContentTypeErrors.Missing);
        }

        // Parameters are transport detail: "text/plain; charset=utf-8" stores as "text/plain".
        var essence = value.Split(';', 2)[0].Trim().ToLowerInvariant();

        if (essence.Length > MaxLength)
        {
            return Result.Failure<MediaContentType>(MediaContentTypeErrors.Malformed);
        }

        var parts = essence.Split('/');

        if (parts.Length != 2 ||
            !IsToken(parts[0]) ||
            !IsToken(parts[1]))
        {
            return Result.Failure<MediaContentType>(MediaContentTypeErrors.Malformed);
        }

        return Result.Success(new MediaContentType(parts[0], parts[1]));
    }

    public static MediaContentType Create(string value) =>
        TryCreate(value) is { IsSuccess: true } result
            ? result.Value
            : throw new ArgumentException($"'{value}' is not a media type.", nameof(value));

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Subtype;
    }

    /// <summary>
    /// Archives are the one <c>application/*</c> subtype worth separating, because they carry
    /// a different size ceiling and a different scanning posture from a PDF.
    /// </summary>
    private MediaKind ApplicationKind() => Subtype switch
    {
        "zip" or "x-zip-compressed" or "x-7z-compressed" or "x-tar" or "gzip" or "x-rar-compressed"
            => MediaKind.Archive,
        _ => MediaKind.Document
    };

    /// <summary>
    /// RFC 6838 restricted-name characters. Rejecting anything else keeps a header value from
    /// carrying a newline or a quote into a stored field and out again in a response header.
    /// </summary>
    private static bool IsToken(string value) =>
        value.Length > 0 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '-' or '+' or '_');
}
