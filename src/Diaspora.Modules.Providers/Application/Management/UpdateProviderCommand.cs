using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Edits a provider's identity and classification.</summary>
[Transactional]
[Auditable]
[RequirePolicy(ProviderPolicies.Manage)]
public sealed record UpdateProviderCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required UpdateProviderRequest Request { get; init; }
}
