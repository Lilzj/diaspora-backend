using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// Pauses, resumes or cancels an upload.
/// <para>
/// One command for the three because they are one decision from the client's point of view —
/// its <c>uploadState</c> reducer already treats them as transitions of a single value — and
/// three commands would be three nearly-identical handlers with the same ownership check.
/// </para>
/// </summary>
[Auditable]
[Authorize(Policy = MediaPolicies.UploadImage)]
public sealed record SetUploadStateCommand : ICommand<UploadSessionResponse>
{
    public required Guid SessionId { get; init; }

    /// <summary><c>paused</c>, <c>uploading</c> (resume) or <c>cancelled</c>.</summary>
    public required string State { get; init; }
}
