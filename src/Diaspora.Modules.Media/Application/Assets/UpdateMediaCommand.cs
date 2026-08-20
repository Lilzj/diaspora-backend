using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Changes an asset's description or audience.</summary>
[Auditable]
public sealed record UpdateMediaCommand : ICommand<MediaAssetResponse>
{
    public required Guid MediaId { get; init; }

    public required UpdateMediaRequest Request { get; init; }
}
