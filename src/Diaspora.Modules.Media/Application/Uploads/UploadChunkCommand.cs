using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// Sends one part of a chunked upload.
/// <para>
/// Carries a <see cref="Stream"/> rather than a byte array. The endpoint hands the request
/// body straight through, so a five-megabyte part is never materialised twice — once in the
/// command and once in the handler — which at any real concurrency is the difference between
/// a working upload path and a memory problem.
/// </para>
/// </summary>
[Authorize(Policy = MediaPolicies.UploadImage)]
public sealed record UploadChunkCommand : ICommand<UploadSessionResponse>
{
    public required Guid SessionId { get; init; }

    /// <summary>One-based part number.</summary>
    public required int ChunkNumber { get; init; }

    public required Stream Content { get; init; }

    /// <summary>
    /// SHA-256 of this part, when the client computed one. Verified on arrival, so a corrupt
    /// part is identified as <em>that</em> part and the client re-sends five megabytes rather
    /// than five hundred.
    /// </summary>
    public string? ChunkHash { get; init; }
}
