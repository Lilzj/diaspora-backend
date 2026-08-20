namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Opens an upload.
/// <para>
/// The client declares what it is about to send and the server decides everything else — the
/// protocol, the chunk size, whether the file is acceptable at all. A client that chose its
/// own protocol would need to know whether this deployment's storage provider supports
/// presigned PUT, which is exactly the provider knowledge the storage abstraction exists to
/// keep out of callers.
/// </para>
/// </summary>
public sealed record StartUploadRequest
{
    /// <summary>
    /// <c>image</c>, <c>video</c>, <c>audio</c>, <c>document</c>, <c>archive</c>,
    /// <c>verificationDocument</c>. Decides the size ceiling, the permission required and the
    /// default visibility.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>The client's <c>mimeType</c>.</summary>
    public required string ContentType { get; init; }

    /// <summary>The name the member's file has. Sanitised server-side; never used in a key.</summary>
    public required string FileName { get; init; }

    /// <summary>The client's <c>sizeBytes</c>. Checked against the assembled file at completion.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// SHA-256 of the whole file, if the client computed one.
    /// <para>
    /// Optional because a streaming client legitimately cannot know it before sending. When
    /// supplied it is verified against the assembled bytes, and a mismatch fails the upload —
    /// which is the only way to detect a part that arrived intact but in the wrong order.
    /// </para>
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// What the file is for, when the client already knows.
    /// <para>
    /// Optional because a housing wizard uploads photographs several steps before the listing
    /// that will own them exists. An unclaimed asset is swept after its grace period, which is
    /// what stops an abandoned wizard leaking storage.
    /// </para>
    /// </summary>
    public string? OwnerType { get; init; }

    public Guid? OwnerId { get; init; }

    /// <summary><c>cover</c>, <c>gallery</c>, <c>avatar</c>, <c>document</c>.</summary>
    public string? Role { get; init; }
}
