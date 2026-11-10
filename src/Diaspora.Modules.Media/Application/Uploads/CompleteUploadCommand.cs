using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// Closes an upload and turns it into an asset.
/// <para>
/// Marked <see cref="TransactionalAttribute"/>: the session's completion, the asset's
/// creation and the processing job's queueing are three writes that must land together. A
/// crash between them leaves either an asset nothing will process or a session that believes
/// it produced an asset that does not exist.
/// </para>
/// </summary>
[Auditable]
[Transactional(Reason =
    "Completing a session writes the session, the asset and the processing job. Partial " +
    "application leaves either an unprocessable asset or a session pointing at nothing.")]
[RequirePolicy(MediaPolicies.UploadImage)]
public sealed record CompleteUploadCommand : ICommand<UploadSessionResponse>
{
    public required Guid SessionId { get; init; }
}
