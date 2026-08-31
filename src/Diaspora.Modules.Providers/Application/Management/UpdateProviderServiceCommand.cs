using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Edits one thing a provider offers.</summary>
[Transactional]
[Auditable]
public sealed record UpdateProviderServiceCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required Guid ServiceId { get; init; }

    public required ProviderServiceRequest Request { get; init; }
}
