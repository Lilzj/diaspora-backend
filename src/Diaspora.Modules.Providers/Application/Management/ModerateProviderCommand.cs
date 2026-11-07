using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Suspends a provider or lifts its suspension, with a reason. Staff only.</summary>
[Transactional]
[Auditable]
public sealed record ModerateProviderCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required ModerateProviderRequest Request { get; init; }
}
