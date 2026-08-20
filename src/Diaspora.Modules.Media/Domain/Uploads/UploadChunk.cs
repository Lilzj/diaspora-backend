using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Uploads;

/// <summary>
/// One received part of a chunked upload.
/// <para>
/// An entity inside <see cref="UploadSession"/>. Parts are never queried across sessions and
/// never outlive theirs, and holding them inside the boundary is what makes the idempotency
/// rule — same number and same digest is a no-op, same number and different digest is a
/// conflict — enforceable in one method rather than at every call site that might re-receive
/// one.
/// </para>
/// <para>
/// The digest is per-part rather than only whole-file, and that is what makes resumption
/// worth having: a corrupt part is identified as <em>that</em> part, and the client re-sends
/// five megabytes instead of five hundred.
/// </para>
/// </summary>
public sealed class UploadChunk : Entity<UploadChunkId>
{
    public UploadSessionId SessionId { get; private set; } = default!;

    /// <summary>One-based part number. Ordering for reassembly.</summary>
    public int Number { get; private set; }

    public ByteSize Size { get; private set; } = ByteSize.Zero;

    public ContentHash Hash { get; private set; } = default!;

    /// <summary>Where the part is staged. Removed once the session completes or expires.</summary>
    public StorageKey Key { get; private set; } = default!;

    public DateTimeOffset ReceivedAt { get; private set; }

    private UploadChunk() { }

    private UploadChunk(
        UploadChunkId id,
        UploadSessionId sessionId,
        int number,
        ByteSize size,
        ContentHash hash,
        StorageKey key,
        DateTimeOffset receivedAt) : base(id)
    {
        SessionId = sessionId;
        Number = number;
        Size = size;
        Hash = hash;
        Key = key;
        ReceivedAt = receivedAt;
    }

    internal static UploadChunk Create(
        UploadSessionId sessionId,
        int number,
        ByteSize size,
        ContentHash hash,
        StorageKey key,
        DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(key);

        return new UploadChunk(
            UploadChunkId.New(), sessionId, number, size, hash, key, receivedAt);
    }
}
