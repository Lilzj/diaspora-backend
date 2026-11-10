using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Adds one thing a provider offers.</summary>
[Transactional]
[Auditable]
[Authorize(Policy = ProviderPolicies.ManageServices)]
public sealed record AddProviderServiceCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required ProviderServiceRequest Request { get; init; }
}
