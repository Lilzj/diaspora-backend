using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Events;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.Uploads;

/// <summary>
/// One upload in flight: what the client said it was sending, which parts have arrived, and
/// what it became.
/// <para>
/// <b>Why uploads are sessions and not a POST.</b> The client's own attachment model has a
/// <c>'paused'</c> state and a 0–100 progress bar, and its composers persist half-finished
/// work to <c>AsyncStorage</c> expecting to resume it later. None of that is reachable through
/// a single request: a POST can be retried, but it cannot be paused, resumed, or resumed on a
/// different device after a reinstall. The session is what makes the UI the client already
/// wrote actually work.
/// </para>
/// <para>
/// <b>The invariant that matters</b> is that a session cannot produce an asset until every
/// declared part has arrived and the reassembled bytes hash to what the client promised. Both
/// halves are load-bearing. Part-count alone would accept a file with a corrupt middle chunk;
/// the hash alone would accept a truncated one whose last chunk never came.
/// </para>
/// <para>
/// Chunks are entities inside this boundary. Nothing queries a chunk across sessions, and
/// "the same part sent twice is idempotent, the same part sent with different bytes is a
/// conflict" is a rule with exactly one home.
/// </para>
/// </summary>
public sealed class UploadSession : AggregateRoot<UploadSessionId>, IAuditableEntity
{
    private readonly List<UploadChunk> _chunks = [];

    public UserId StartedBy { get; private set; } = default!;

    public UploadSessionStatus Status { get; private set; }

    public UploadProtocol Protocol { get; private set; }

    /// <summary>The kind the client asked to upload. Verified against the bytes on completion.</summary>
    public MediaKind Kind { get; private set; }

    public MediaContentType DeclaredContentType { get; private set; } = default!;

    public SafeFileName FileName { get; private set; } = default!;

    /// <summary>How large the client said the file is. The ceiling check runs against this.</summary>
    public ByteSize DeclaredSize { get; private set; } = ByteSize.Zero;

    /// <summary>
    /// The digest the client says the whole file has, when it knows. Optional because a
    /// streaming client legitimately cannot compute it before sending — but when supplied it
    /// is checked, and a mismatch fails the session.
    /// </summary>
    public ContentHash? DeclaredHash { get; private set; }

    public int TotalChunks { get; private set; }

    public ByteSize ChunkSize { get; private set; } = ByteSize.Zero;

    /// <summary>Where the parts are being staged.</summary>
    public string StorageProvider { get; private set; } = default!;

    public string StorageContainer { get; private set; } = default!;

    /// <summary>
    /// What the session was uploaded for, when the client knew at the start.
    /// <para>
    /// Optional on purpose: a housing wizard uploads photographs several steps before the
    /// listing that will own them exists. The asset is attached later, and the sweeper's grace
    /// period is what keeps an abandoned wizard from leaking storage.
    /// </para>
    /// </summary>
    public MediaOwner Owner { get; private set; } = MediaOwner.Unassigned;

    public string? Role { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The asset this session produced. Null until it completes.</summary>
    public MediaId? MediaId { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<UploadChunk> Chunks => _chunks.AsReadOnly();

    /// <summary>Part numbers already held, ascending. What a resuming client asks for.</summary>
    public IReadOnlyList<int> ReceivedChunkNumbers =>
        _chunks.Select(chunk => chunk.Number).Order().ToList().AsReadOnly();

    public ByteSize ReceivedSize =>
        _chunks.Aggregate(ByteSize.Zero, (total, chunk) => total.Add(chunk.Size));

    /// <summary>
    /// Completion as a whole percentage — the client's <c>uploadProgress</c>, in the units its
    /// progress bar already renders.
    /// </summary>
    public int ProgressPercent =>
        DeclaredSize.Bytes == 0
            ? 0
            : (int)Math.Min(100, ReceivedSize.Bytes * 100 / DeclaredSize.Bytes);

    public bool IsComplete => TotalChunks > 0 && _chunks.Count == TotalChunks;

    public bool IsOpen => Status is UploadSessionStatus.Queued
        or UploadSessionStatus.Uploading
        or UploadSessionStatus.Paused;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private UploadSession() { }

    private UploadSession(
        UploadSessionId id,
        UserId startedBy,
        MediaKind kind,
        UploadProtocol protocol,
        MediaContentType declaredContentType,
        SafeFileName fileName,
        ByteSize declaredSize,
        ContentHash? declaredHash,
        int totalChunks,
        ByteSize chunkSize,
        string storageProvider,
        string storageContainer,
        DateTimeOffset expiresAt) : base(id)
    {
        StartedBy = startedBy;
        Kind = kind;
        Protocol = protocol;
        DeclaredContentType = declaredContentType;
        FileName = fileName;
        DeclaredSize = declaredSize;
        DeclaredHash = declaredHash;
        TotalChunks = totalChunks;
        ChunkSize = chunkSize;
        StorageProvider = storageProvider;
        StorageContainer = storageContainer;
        ExpiresAt = expiresAt;
        Status = UploadSessionStatus.Queued;
    }

    /// <summary>
    /// Opens an upload, validating everything that can be known before a byte has arrived.
    /// <para>
    /// Deliberately front-loaded. Refusing a 600 MB video at initialisation costs one round
    /// trip; refusing it after the bytes have crossed a mobile network costs the member their
    /// data allowance and their patience, and costs the platform the bandwidth either way.
    /// </para>
    /// </summary>
    public static Result<UploadSession> Start(
        UserId startedBy,
        MediaKind kind,
        MediaContentType declaredContentType,
        SafeFileName fileName,
        ByteSize declaredSize,
        ContentHash? declaredHash,
        string storageProvider,
        string storageContainer,
        DateTimeOffset now,
        ByteSize? directCeiling = null,
        ByteSize? chunkSize = null,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(startedBy);
        ArgumentNullException.ThrowIfNull(declaredContentType);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(declaredSize);

        if (declaredSize.Bytes <= 0)
        {
            return Result.Failure<UploadSession>(MediaErrors.UploadEmpty);
        }

        var ceiling = MediaLimits.MaxSizeFor(kind);

        if (declaredSize.Exceeds(ceiling))
        {
            return Result.Failure<UploadSession>(MediaErrors.TooLarge(declaredSize, ceiling));
        }

        if (!MediaTypeAllowList.AllowsContentType(kind, declaredContentType))
        {
            return Result.Failure<UploadSession>(
                MediaErrors.ContentTypeNotAllowed(declaredContentType));
        }

        if (!MediaTypeAllowList.AllowsExtension(kind, fileName.Extension))
        {
            return Result.Failure<UploadSession>(
                MediaErrors.ExtensionNotAllowed(fileName.Extension));
        }

        var direct = directCeiling ?? MediaLimits.DirectUploadCeiling;
        var part = chunkSize ?? MediaLimits.ChunkSize;

        // The protocol is the server's decision, not the client's. A client that had to choose
        // would need to know whether this deployment's provider supports presigned PUT — the
        // exact provider knowledge the storage abstraction exists to keep out of callers.
        var protocol = declaredSize.Exceeds(direct) ? UploadProtocol.Chunked : UploadProtocol.Direct;

        var totalChunks = protocol is UploadProtocol.Direct
            ? 1
            : (int)Math.Ceiling(declaredSize.Bytes / (double)part.Bytes);

        if (totalChunks > MediaLimits.MaxChunksPerSession)
        {
            return Result.Failure<UploadSession>(MediaErrors.TooLarge(declaredSize, ceiling));
        }

        var session = new UploadSession(
            UploadSessionId.New(),
            startedBy,
            kind,
            protocol,
            declaredContentType,
            fileName,
            declaredSize,
            declaredHash,
            totalChunks,
            protocol is UploadProtocol.Direct ? declaredSize : part,
            storageProvider,
            storageContainer,
            now.Add(lifetime ?? MediaLimits.UploadSessionLifetime));

        session.RaiseDomainEvent(new UploadSessionStartedDomainEvent
        {
            SessionId = session.Id,
            StartedBy = startedBy,
            Kind = kind,
            Protocol = protocol,
            DeclaredSizeInBytes = declaredSize.Bytes
        });

        return Result.Success(session);
    }

    /// <summary>Records what the upload will eventually be attached to.</summary>
    public void IntendFor(MediaOwner owner, string? role)
    {
        ArgumentNullException.ThrowIfNull(owner);

        Owner = owner;
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();
    }

    // ── Receiving ────────────────────────────────────────────────────────

    /// <summary>
    /// Records one received part.
    /// <para>
    /// Idempotent by design: re-sending a part whose digest matches what is already held
    /// succeeds and changes nothing. That is what makes the client's retry safe — a part whose
    /// response was lost on the way back gets sent again, and the second attempt must not be a
    /// conflict. Re-sending the <em>same</em> part with <em>different</em> bytes is a genuine
    /// conflict and is refused.
    /// </para>
    /// </summary>
    public Result<UploadChunk> ReceiveChunk(
        int number, ByteSize size, ContentHash hash, StorageKey key, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(key);

        if (now >= ExpiresAt)
        {
            Expire();
            return Result.Failure<UploadChunk>(MediaErrors.UploadSessionExpired);
        }

        if (!IsOpen)
        {
            return Result.Failure<UploadChunk>(MediaErrors.UploadSessionNotOpen);
        }

        if (number < 1 || number > TotalChunks)
        {
            return Result.Failure<UploadChunk>(MediaErrors.ChunkOutOfRange);
        }

        var existing = _chunks.FirstOrDefault(chunk => chunk.Number == number);

        if (existing is not null)
        {
            return existing.Hash.Matches(hash)
                ? Result.Success(existing)
                : Result.Failure<UploadChunk>(MediaErrors.ChunkChecksumMismatch);
        }

        // Every part but the last must be full-size. Without a floor a client can open a
        // session and send ten thousand one-byte parts, each of which is a database row and a
        // stored object.
        var isFinalChunk = number == TotalChunks;

        if (!isFinalChunk && size.Bytes < MediaLimits.MinimumChunkSize.Bytes)
        {
            return Result.Failure<UploadChunk>(
                MediaErrors.ChunkTooSmall(MediaLimits.MinimumChunkSize));
        }

        var projected = ReceivedSize.Add(size);

        if (projected.Exceeds(DeclaredSize))
        {
            return Result.Failure<UploadChunk>(MediaErrors.UploadSizeMismatch);
        }

        var chunk = UploadChunk.Create(Id, number, size, hash, key, now);
        _chunks.Add(chunk);

        Status = UploadSessionStatus.Uploading;

        return Result.Success(chunk);
    }

    /// <summary>Pauses. Parts already held are kept; the expiry clock keeps running.</summary>
    public Result Pause()
    {
        if (!IsOpen)
        {
            return Result.Failure(MediaErrors.UploadSessionNotOpen);
        }

        Status = UploadSessionStatus.Paused;
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status is not UploadSessionStatus.Paused)
        {
            return Result.Failure(MediaErrors.UploadSessionNotOpen);
        }

        Status = _chunks.Count == 0 ? UploadSessionStatus.Queued : UploadSessionStatus.Uploading;
        return Result.Success();
    }

    /// <summary>
    /// Closes the session against the asset it produced.
    /// <para>
    /// The one place the two completeness checks meet. Every declared part must be present,
    /// and — when the client declared one — the reassembled digest must match. A session that
    /// satisfies only the first has a corrupt part; one that satisfies only the second cannot
    /// happen. Both, and only both, produce an asset.
    /// </para>
    /// </summary>
    public Result Complete(MediaId mediaId, ContentHash assembledHash, ByteSize assembledSize)
    {
        ArgumentNullException.ThrowIfNull(mediaId);
        ArgumentNullException.ThrowIfNull(assembledHash);
        ArgumentNullException.ThrowIfNull(assembledSize);

        if (Status is UploadSessionStatus.Completed)
        {
            return Result.Success();
        }

        if (!IsOpen)
        {
            return Result.Failure(MediaErrors.UploadSessionNotOpen);
        }

        if (!IsComplete)
        {
            return Result.Failure(MediaErrors.UploadIncomplete);
        }

        if (assembledSize.Bytes != DeclaredSize.Bytes)
        {
            Fail("The assembled file was not the declared size.");
            return Result.Failure(MediaErrors.UploadSizeMismatch);
        }

        if (DeclaredHash is not null && !DeclaredHash.Matches(assembledHash))
        {
            Fail("The assembled file did not match the declared checksum.");
            return Result.Failure(MediaErrors.ChunkChecksumMismatch);
        }

        MediaId = mediaId;
        Status = UploadSessionStatus.Completed;

        RaiseDomainEvent(new UploadSessionCompletedDomainEvent
        {
            SessionId = Id,
            MediaId = mediaId,
            ChunkCount = _chunks.Count
        });

        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is UploadSessionStatus.Completed)
        {
            return Result.Failure(MediaErrors.UploadSessionNotOpen);
        }

        Status = UploadSessionStatus.Cancelled;
        return Result.Success();
    }

    public void Fail(string reason)
    {
        if (Status is UploadSessionStatus.Completed)
        {
            return;
        }

        Status = UploadSessionStatus.Failed;
        FailureReason = reason.Length <= 500 ? reason : reason[..500];
    }

    public void Expire()
    {
        if (IsOpen)
        {
            Status = UploadSessionStatus.Expired;
        }
    }

    /// <summary>The storage keys of every staged part, in order. What assembly reads.</summary>
    public IReadOnlyList<StorageKey> OrderedChunkKeys() =>
        _chunks.OrderBy(chunk => chunk.Number).Select(chunk => chunk.Key).ToList().AsReadOnly();

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
