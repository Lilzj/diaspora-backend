using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Asks for a rendition that does not exist yet.</summary>
[Auditable]
public sealed record RequestVariantCommand : ICommand<MediaAssetResponse>
{
    public required Guid MediaId { get; init; }

    public required RequestVariantRequest Request { get; init; }
}
