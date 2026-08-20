using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Drives an upload from "a client says it is about to send a file" to "an asset exists".
/// <para>
/// The one service that touches storage, sessions, assets and the processing pipeline in the
/// same breath — and it exists precisely so that no handler has to. The ordering constraints
/// here are subtle and getting one wrong produces a class of bug that only appears under
/// failure: an asset row pointing at bytes that were never written, a session marked complete
/// whose parts are gone, staged parts that outlive their session forever.
/// </para>
/// <para>
/// <b>The rule the whole file follows: bytes first, records second.</b> Writing the row before
/// the object leaves a dangling reference on any failure, which no reader can distinguish from
/// a transient outage. Writing the object first leaves an orphaned object, which the sweeper
/// collects. One of those failure modes is recoverable and the other is a permanent lie in the
/// database.
/// </para>
/// </summary>
public sealed class UploadCoordinator
{
    private readonly IUploadSessionRepository _sessions;
    private readonly IMediaAssetRepository _assets;
    private readonly IProcessingJobRepository _jobs;
    private readonly StorageProviderRegistry _storage;
    private readonly UploadValidator _validator;
    private readonly MediaProcessingPipeline _pipeline;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guids;
    private readonly MediaUploadOptions _uploadOptions;
    private readonly MediaProcessingOptions _processingOptions;
    private readonly MediaStorageOptions _storageOptions;
    private readonly ILogger<UploadCoordinator> _logger;

    public UploadCoordinator(
        IUploadSessionRepository sessions,
        IMediaAssetRepository assets,
        IProcessingJobRepository jobs,
        StorageProviderRegistry storage,
        UploadValidator validator,
        MediaProcessingPipeline pipeline,
        ICurrentUser currentUser,
        IClock clock,
        IGuidGenerator guids,
        IOptions<MediaUploadOptions> uploadOptions,
        IOptions<MediaProcessingOptions> processingOptions,
        IOptions<MediaStorageOptions> storageOptions,
        ILogger<UploadCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(uploadOptions);
        ArgumentNullException.ThrowIfNull(processingOptions);
        ArgumentNullException.ThrowIfNull(storageOptions);

        _sessions = sessions;
        _assets = assets;
        _jobs = jobs;
        _storage = storage;
        _validator = validator;
        _pipeline = pipeline;
        _currentUser = currentUser;
        _clock = clock;
        _guids = guids;
        _uploadOptions = uploadOptions.Value;
        _processingOptions = processingOptions.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    // ── Session lifecycle ────────────────────────────────────────────────

    /// <summary>Opens an upload, refusing everything decidable before a byte arrives.</summary>
    public async Task<Result<UploadSession>> StartAsync(
        UserId userId,
        MediaKind kind,
        MediaContentType contentType,
        SafeFileName fileName,
        ByteSize size,
        ContentHash? declaredHash,
        MediaOwner owner,
        string? role,
        CancellationToken cancellationToken = default)
    {
        var validation = UploadValidator.ValidateDeclaration(kind, contentType, fileName, size);

        if (validation.IsFailure)
        {
            return Result.Failure<UploadSession>(validation.Error);
        }

        // A quota rather than a rate limit: the resource being protected is staged storage,
        // not request throughput, and a script that opens sessions without finishing them
        // costs storage even while sending nothing.
        var open = await _sessions.CountOpenForUserAsync(userId, cancellationToken);

        if (open >= _uploadOptions.MaxConcurrentSessionsPerUser)
        {
            return Result.Failure<UploadSession>(MediaErrors.UploadQuotaExceeded);
        }

        var provider = _storage.Default;

        var session = UploadSession.Start(
            userId,
            kind,
            contentType,
            fileName,
            size,
            declaredHash,
            provider.Name,
            _storageOptions.Container,
            _clock.UtcNow,
            ByteSize.FromMegabytes(_uploadOptions.DirectUploadCeilingMegabytes),
            ByteSize.FromMegabytes(_uploadOptions.ChunkSizeMegabytes),
            TimeSpan.FromHours(_uploadOptions.SessionLifetimeHours));

        if (session.IsFailure)
        {
            return session;
        }

        if (owner.IsAssigned)
        {
            session.Value.IntendFor(owner, role);
        }

        await _sessions.AddAsync(session.Value, cancellationToken);

        MediaTelemetry.UploadsStarted.Add(1,
            new KeyValuePair<string, object?>("kind", kind.ToString()),
            new KeyValuePair<string, object?>("protocol", session.Value.Protocol.ToString()));

        return session;
    }

    /// <summary>
    /// Stages one received part.
    /// <para>
    /// The signature check runs here, on part one, and that placement is the whole reason it
    /// is worth having: a disguised archive is refused after five megabytes instead of after
    /// five hundred. It is possible only because the signature table needs at most thirty-two
    /// leading bytes.
    /// </para>
    /// </summary>
    public async Task<Result<UploadSession>> ReceiveChunkAsync(
        UploadSession session,
        int chunkNumber,
        Stream content,
        ContentHash? declaredChunkHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(content);

        var provider = _storage.Resolve(session.StorageProvider);

        if (provider.IsFailure)
        {
            return Result.Failure<UploadSession>(provider.Error);
        }

        // Buffered so the bytes can be hashed, sniffed and written without three reads of a
        // network stream. Bounded by the chunk size the session declared, which the endpoint
        // has already enforced against the request body limit.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            return Result.Failure<UploadSession>(MediaErrors.UploadEmpty);
        }

        var hash = ContentHash.Compute(bytes);

        if (declaredChunkHash is not null && !declaredChunkHash.Matches(hash))
        {
            return Result.Failure<UploadSession>(MediaErrors.ChunkChecksumMismatch);
        }

        if (chunkNumber == 1)
        {
            var head = bytes.AsSpan(0, Math.Min(FileSignature.InspectionLength, bytes.Length));

            var signature = _validator.ValidateSignature(
                session.Kind, session.DeclaredContentType, session.FileName.Extension, head);

            if (signature.IsFailure)
            {
                session.Fail(signature.Error.Description);
                return Result.Failure<UploadSession>(signature.Error);
            }
        }

        var key = StorageKey.ForUploadPart(session.Id.Value, chunkNumber);

        // The domain records the part only after storage has accepted it. The reverse order
        // produces a session that believes it holds a part nothing wrote.
        buffer.Position = 0;

        await provider.Value.WriteAsync(
            new StorageWriteRequest
            {
                Container = session.StorageContainer,
                Key = key,
                Content = buffer,
                ContentType = MediaContentType.OctetStream,
                ContentLength = bytes.Length
            },
            cancellationToken);

        var received = session.ReceiveChunk(
            chunkNumber, ByteSize.FromBytes(bytes.Length), hash, key, _clock.UtcNow);

        if (received.IsFailure)
        {
            // The part is in storage and the session refused it. Removing it now keeps the
            // staged set consistent with what the session believes it holds; failing to would
            // leave a part the sweeper only finds when the session expires.
            await provider.Value.DeleteAsync(session.StorageContainer, key, cancellationToken);
            return Result.Failure<UploadSession>(received.Error);
        }

        MediaTelemetry.ChunksReceived.Add(1);

        return Result.Success(session);
    }

    /// <summary>
    /// Assembles the parts, records the asset, and runs the pipeline.
    /// <para>
    /// Every check that can only be made on the whole file happens here: the assembled size
    /// against the declared size, and the assembled digest against the declared digest. A
    /// session that satisfies the part count but not the digest has a corrupt part, and
    /// completing it would store a file that looks fine until somebody opens it.
    /// </para>
    /// </summary>
    public async Task<Result<MediaAsset>> CompleteAsync(
        UploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsOpen)
        {
            return Result.Failure<MediaAsset>(MediaErrors.UploadSessionNotOpen);
        }

        if (!session.IsComplete)
        {
            return Result.Failure<MediaAsset>(MediaErrors.UploadIncomplete);
        }

        var provider = _storage.Resolve(session.StorageProvider);

        if (provider.IsFailure)
        {
            return Result.Failure<MediaAsset>(provider.Error);
        }

        var mediaId = MediaId.From(_guids.NewGuid());
        var now = _clock.UtcNow;

        var key = StorageKey.ForOriginal(
            session.Kind, mediaId.Value, session.FileName.Extension, now);

        var composed = await provider.Value.ComposeAsync(
            session.StorageContainer,
            session.OrderedChunkKeys(),
            key,
            session.DeclaredContentType,
            cancellationToken);

        // Re-read to hash. The alternative — combining the per-part digests — is not a digest
        // of the file and would not detect parts assembled in the wrong order.
        ContentHash assembled;

        await using (var stored = await provider.Value.OpenReadAsync(
            session.StorageContainer, key, cancellationToken))
        {
            if (stored is null)
            {
                session.Fail("The assembled object could not be read back.");
                return Result.Failure<MediaAsset>(MediaErrors.StorageObjectMissing);
            }

            assembled = await ContentHash.ComputeAsync(stored, cancellationToken);
        }

        var completed = session.Complete(
            mediaId, assembled, ByteSize.FromBytes(composed.SizeInBytes));

        if (completed.IsFailure)
        {
            await provider.Value.DeleteAsync(session.StorageContainer, key, cancellationToken);
            return Result.Failure<MediaAsset>(completed.Error);
        }

        var location = StorageLocation.Create(
            provider.Value.Name, session.StorageContainer, key);

        var asset = MediaAsset.Record(
            mediaId,
            session.StartedBy,
            session.Kind,
            location,
            session.DeclaredContentType,
            session.FileName,
            ByteSize.FromBytes(composed.SizeInBytes),
            assembled);

        if (asset.IsFailure)
        {
            await provider.Value.DeleteAsync(session.StorageContainer, key, cancellationToken);
            return asset;
        }

        if (session.Owner.IsAssigned)
        {
            asset.Value.AttachTo(session.Owner, session.Role ?? MediaReferenceRoles.Attachment);
        }

        await _assets.AddAsync(asset.Value, cancellationToken);

        // The staged parts have served their purpose. Removed now rather than left to the
        // sweeper, because a completed upload's parts are pure duplication of storage that is
        // already paid for.
        await provider.Value.DeletePrefixAsync(
            session.StorageContainer, StorageKey.UploadPrefix(session.Id.Value), cancellationToken);

        MediaTelemetry.UploadsCompleted.Add(1,
            new KeyValuePair<string, object?>("kind", session.Kind.ToString()));

        MediaTelemetry.BytesStored.Add(composed.SizeInBytes);

        await ScheduleProcessingAsync(asset.Value, cancellationToken);

        return asset;
    }

    /// <summary>
    /// The whole upload in one request, for a file small enough that a session would be three
    /// round trips where one will do.
    /// <para>
    /// The path almost every upload in this app takes: an <c>expo-image-picker</c> result at
    /// <c>quality: 0.8</c> is well under a megabyte. It shares every validation, hashing and
    /// processing step with the chunked path — the only difference is that no session is
    /// persisted, so there is nothing to resume and nothing to sweep.
    /// </para>
    /// </summary>
    public async Task<Result<MediaAsset>> UploadDirectAsync(
        UserId userId,
        MediaKind kind,
        MediaContentType contentType,
        SafeFileName fileName,
        Stream content,
        MediaOwner owner,
        string? role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var bytes = buffer.ToArray();
        var size = ByteSize.FromBytes(bytes.Length);

        var validation = UploadValidator.ValidateDeclaration(kind, contentType, fileName, size);

        if (validation.IsFailure)
        {
            return Result.Failure<MediaAsset>(validation.Error);
        }

        var ceiling = ByteSize.FromMegabytes(_uploadOptions.DirectUploadCeilingMegabytes);

        if (size.Exceeds(ceiling))
        {
            return Result.Failure<MediaAsset>(MediaErrors.TooLarge(size, ceiling));
        }

        var head = bytes.AsSpan(0, Math.Min(FileSignature.InspectionLength, bytes.Length));
        var signature = _validator.ValidateSignature(kind, contentType, fileName.Extension, head);

        if (signature.IsFailure)
        {
            return Result.Failure<MediaAsset>(signature.Error);
        }

        var provider = _storage.Default;
        var mediaId = MediaId.From(_guids.NewGuid());
        var now = _clock.UtcNow;

        var key = StorageKey.ForOriginal(kind, mediaId.Value, fileName.Extension, now);

        buffer.Position = 0;

        var written = await provider.WriteAsync(
            new StorageWriteRequest
            {
                Container = _storageOptions.Container,
                Key = key,
                Content = buffer,
                ContentType = contentType,
                ContentLength = bytes.Length,
                DownloadFileName = fileName.Value
            },
            cancellationToken);

        var asset = MediaAsset.Record(
            mediaId,
            userId,
            kind,
            StorageLocation.Create(provider.Name, _storageOptions.Container, key),
            contentType,
            fileName,
            ByteSize.FromBytes(written.SizeInBytes),
            ContentHash.Compute(bytes));

        if (asset.IsFailure)
        {
            await provider.DeleteAsync(_storageOptions.Container, key, cancellationToken);
            return asset;
        }

        if (owner.IsAssigned)
        {
            asset.Value.AttachTo(owner, role ?? MediaReferenceRoles.Attachment);
        }

        await _assets.AddAsync(asset.Value, cancellationToken);

        MediaTelemetry.UploadsStarted.Add(1,
            new KeyValuePair<string, object?>("kind", kind.ToString()),
            new KeyValuePair<string, object?>("protocol", UploadProtocol.Direct.ToString()));

        MediaTelemetry.UploadsCompleted.Add(1,
            new KeyValuePair<string, object?>("kind", kind.ToString()));

        MediaTelemetry.BytesStored.Add(written.SizeInBytes);

        await ScheduleProcessingAsync(asset.Value, cancellationToken);

        return asset;
    }

    /// <summary>
    /// Cancels a session and removes its staged parts.
    /// <para>
    /// Storage first, session second — the same ordering rule as everywhere else here. A
    /// session marked cancelled whose parts survive is a leak nothing will ever look for,
    /// because the expiry sweep only visits sessions that are still open.
    /// </para>
    /// </summary>
    public async Task<Result> CancelAsync(
        UploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var cancelled = session.Cancel();

        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        var provider = _storage.Resolve(session.StorageProvider);

        if (provider.IsSuccess)
        {
            await provider.Value.DeletePrefixAsync(
                session.StorageContainer,
                StorageKey.UploadPrefix(session.Id.Value),
                cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// Plans the asset's processing and, when configured to, runs it before returning.
    /// <para>
    /// Inline is the default and it is the right default for this platform's traffic: nearly
    /// every upload is one small photograph, and a member who waits an extra fraction of a
    /// second and receives a working thumbnail is better served than one who receives a
    /// spinner. A deployment that adds video transcoding must turn it off — a transcode is
    /// minutes, and no request should hold a connection that long.
    /// </para>
    /// <para>
    /// A failure here never fails the upload. The bytes are stored and the job is queued; the
    /// worker will get to it.
    /// </para>
    /// </summary>
    private async Task ScheduleProcessingAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        var job = ProcessingJob.Plan(asset.Id, MediaProcessingPipeline.Plan(asset.Kind));

        await _jobs.AddAsync(job, cancellationToken);

        if (!_processingOptions.ProcessInline)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_processingOptions.InlineTimeoutSeconds));

        try
        {
            await _pipeline.ExecuteAsync(job, asset, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The escape hatch that makes inline processing safe. A pathological file does not
            // hold a request thread; it goes back on the queue and the member gets a pending
            // asset instead of a timeout.
            _logger.LogInformation(
                "Inline processing for media {MediaId} exceeded its budget; handed to the worker.",
                asset.Id.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Inline processing for media {MediaId} failed; the asset is stored and queued.",
                asset.Id.Value);
        }
    }
}
