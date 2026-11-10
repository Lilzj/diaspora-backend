using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Reads the most recent processing job for an asset.
/// <para>
/// The answer to "why does my photograph have no thumbnail" — which is either "the deployment
/// has no codec" or "the codec failed", and those want different responses.
/// </para>
/// </summary>
[RequirePolicy(MediaPolicies.Read)]
public sealed record GetProcessingJobQuery : IQuery<ProcessingJobResponse>
{
    public required Guid MediaId { get; init; }
}
