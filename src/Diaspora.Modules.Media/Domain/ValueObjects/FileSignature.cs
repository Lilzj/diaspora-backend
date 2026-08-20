using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// What the first bytes of a file say it actually is.
/// <para>
/// The declared content type and the file extension are both claims made by the caller, and
/// both are trivially forged. This is the only statement about a file's format that comes from
/// the file. An upload whose signature disagrees with its declared type is refused — that one
/// rule closes polyglot files, the <c>shell.php.jpg</c> family, and the "rename it to .jpg and
/// the CDN will serve it as script" trick in a single check.
/// </para>
/// <para>
/// The table is deliberately narrow. Every entry is a format the frontend contract report
/// found the client actually produces or displays; a signature table that recognises fifty
/// formats is a table where nobody can tell which entries are load-bearing.
/// </para>
/// </summary>
public sealed class FileSignature : ValueObject
{
    /// <summary>How many leading bytes are needed to identify every format in the table.</summary>
    public const int InspectionLength = 32;

    /// <summary>The answer for bytes that match nothing known.</summary>
    public static readonly FileSignature Unrecognized =
        new(MediaKind.Unknown, ImageFormat.Unknown, MediaContentType.OctetStream, string.Empty);

    private FileSignature(
        MediaKind kind, ImageFormat imageFormat, MediaContentType contentType, string description)
    {
        Kind = kind;
        ImageFormat = imageFormat;
        ContentType = contentType;
        Description = description;
    }

    public MediaKind Kind { get; }

    /// <summary>The container format, when the file is an image.</summary>
    public ImageFormat ImageFormat { get; }

    /// <summary>The content type the bytes themselves imply.</summary>
    public MediaContentType ContentType { get; }

    /// <summary>A short human-readable name, for error messages and audit lines.</summary>
    public string Description { get; }

    public bool IsRecognized => Kind != MediaKind.Unknown;

    internal static FileSignature For(
        MediaKind kind, ImageFormat imageFormat, string contentType, string description) =>
        new(kind, imageFormat, MediaContentType.Create(contentType), description);

    /// <summary>
    /// Whether a declared content type is consistent with these bytes.
    /// <para>
    /// Compared on the top-level type rather than the exact subtype, because the client's
    /// declaration is routinely close-but-not-exact — an iOS picker sends <c>image/jpg</c> for
    /// a JPEG, and a browser sends <c>image/x-png</c> for a PNG. Refusing those would refuse
    /// real uploads. Refusing <c>image/*</c> for something whose bytes are a ZIP archive is the
    /// check that matters, and it is the check this makes.
    /// </para>
    /// </summary>
    public bool IsConsistentWith(MediaContentType declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (!IsRecognized)
        {
            return false;
        }

        return string.Equals(ContentType.Type, declared.Type, StringComparison.Ordinal);
    }

    public override string ToString() => Description;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return ImageFormat;
        yield return ContentType;
    }
}
