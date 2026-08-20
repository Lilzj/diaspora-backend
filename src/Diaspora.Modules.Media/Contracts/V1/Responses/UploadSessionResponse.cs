namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// An upload in flight.
/// <para>
/// <b>Written to fit the client's existing reducer.</b> <see cref="Status"/> carries the same
/// five values as the frontend's <c>AttachmentUploadState</c> — <c>queued</c>,
/// <c>uploading</c>, <c>paused</c>, <c>completed</c>, <c>failed</c> — and
/// <see cref="ProgressPercent"/> is the 0–100 number its progress bar already renders. A
/// client wiring this up changes where the values come from, not how it handles them.
/// </para>
/// <para>
/// <see cref="ReceivedChunks"/> is what makes resumption work: a client that lost its
/// connection asks for the session, sees which parts arrived, and sends only the gaps. It is
/// also what a client uses after a reinstall, when the local <c>file://</c> URIs it saved to
/// <c>AsyncStorage</c> no longer resolve.
/// </para>
/// </summary>
public sealed record UploadSessionResponse
{
    public required Guid SessionId { get; init; }

    /// <summary><c>queued</c>, <c>uploading</c>, <c>paused</c>, <c>completed</c>, <c>failed</c>, <c>cancelled</c>, <c>expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary><c>direct</c>, <c>chunked</c>, <c>multipart</c>, <c>presignedDirect</c>. Chosen by the server.</summary>
    public required string Protocol { get; init; }

    public required string Kind { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public required int TotalChunks { get; init; }

    public required long ChunkSizeBytes { get; init; }

    /// <summary>Part numbers already held, ascending. Send the gaps.</summary>
    public required IReadOnlyList<int> ReceivedChunks { get; init; }

    /// <summary>0–100. The client's <c>uploadProgress</c>.</summary>
    public required int ProgressPercent { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Where to send the next part. Null once the session has closed.</summary>
    public string? UploadUrl { get; init; }

    /// <summary>The asset produced. Null until the session completes.</summary>
    public MediaAssetResponse? Media { get; init; }

    /// <summary>Why it failed, when it did.</summary>
    public string? FailureReason { get; init; }
}
