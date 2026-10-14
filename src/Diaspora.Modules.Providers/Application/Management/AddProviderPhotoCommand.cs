using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Puts an uploaded photograph on a provider: the cover, or the gallery.</summary>
[Transactional]
[Auditable]
public sealed record AddProviderPhotoCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required ProviderGalleryItemRequest Request { get; init; }
}
