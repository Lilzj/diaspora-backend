using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Runs an asset's processing again. Staff only.
/// <para>
/// The operational lever for the two cases that actually happen: a codec was deployed after
/// the upload, so the renditions never existed; or a transient failure exhausted the retries
/// and somebody has since fixed the cause.
/// </para>
/// </summary>
[Auditable]
public sealed record RequeueProcessingCommand : ICommand<ProcessingJobResponse>
{
    public required Guid MediaId { get; init; }
}
