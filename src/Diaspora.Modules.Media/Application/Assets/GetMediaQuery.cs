using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>Reads one asset's record: its metadata, its variants and its URLs.</summary>
public sealed record GetMediaQuery : IQuery<MediaAssetResponse>
{
    public required Guid MediaId { get; init; }
}
