using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// Reads an upload's state.
/// <para>
/// The resumption endpoint. A client that lost its connection asks for this, reads
/// <c>receivedChunks</c>, and sends only the gaps.
/// </para>
/// </summary>
[Authorize(Policy = MediaPolicies.Read)]
public sealed record GetUploadSessionQuery : IQuery<UploadSessionResponse>
{
    public required Guid SessionId { get; init; }
}
