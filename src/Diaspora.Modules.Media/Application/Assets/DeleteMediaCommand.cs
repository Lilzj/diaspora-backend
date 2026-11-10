using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Removes an asset.
/// <para>
/// Soft. The row survives so an audit can answer "what was attached to that listing before it
/// was edited", and the bytes go on the sweeper's schedule — which is also what makes deletion
/// fast enough to be synchronous even for a video.
/// </para>
/// </summary>
[Auditable]
[Authorize(Policy = MediaPolicies.Delete)]
public sealed record DeleteMediaCommand : ICommand
{
    public required Guid MediaId { get; init; }
}
