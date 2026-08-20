using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Publishes, pauses, closes, suspends, reinstates or archives a provider.</summary>
[Transactional]
[Auditable]
public sealed record ChangeProviderStatusCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required ProviderStatusRequest Request { get; init; }
}
