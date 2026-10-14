using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Changes a provider's description, contact methods or location. See the request.</summary>
[Transactional]
[Auditable]
public sealed record UpdateProviderDetailsCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required UpdateProviderDetailsRequest Request { get; init; }
}

/// <summary>Takes a photograph off a provider: out of the gallery, and off the cover if it was.</summary>
[Transactional]
[Auditable]
public sealed record RemoveProviderPhotoCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required Guid MediaId { get; init; }
}
