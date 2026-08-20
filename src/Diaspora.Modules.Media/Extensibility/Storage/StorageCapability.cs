namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>
/// What a storage provider can do beyond the basics every provider must support.
/// <para>
/// Flags rather than a hierarchy because the capabilities are genuinely independent: a
/// provider can compose server-side without signing URLs, and vice versa. A hierarchy would
/// force an ordering that does not exist.
/// </para>
/// <para>
/// Declaring a capability a provider does not have is the one way to break this seam badly —
/// the caller will plan a presigned upload and the client will PUT to a URL that does not
/// work. The architecture suite asserts the built-in provider's flags for exactly that reason.
/// </para>
/// </summary>
[Flags]
public enum StorageCapability
{
    /// <summary>Read, write, delete. Every provider supports these; they are not flags.</summary>
    None = 0,

    /// <summary>Can mint a time-limited URL a client may GET directly.</summary>
    PresignedRead = 1,

    /// <summary>Can mint a time-limited URL a client may PUT directly.</summary>
    PresignedWrite = 2,

    /// <summary>Can assemble staged parts without the bytes passing through the application.</summary>
    ServerSideCompose = 4,

    /// <summary>Can serve a byte range without reading the whole object.</summary>
    RangeReads = 8,

    /// <summary>Can move an object between hot, cool and archive tiers.</summary>
    StorageTiers = 16,

    /// <summary>Can copy an object without it passing through the application.</summary>
    ServerSideCopy = 32,

    /// <summary>Keeps previous versions of an overwritten object.</summary>
    Versioning = 64
}
