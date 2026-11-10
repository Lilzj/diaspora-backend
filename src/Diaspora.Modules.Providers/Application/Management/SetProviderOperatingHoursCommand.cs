using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>Replaces a provider's opening hours for the days named in the request.</summary>
[Transactional]
[Auditable]
[Authorize(Policy = ProviderPolicies.ManageAvailability)]
public sealed record SetProviderOperatingHoursCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required SetProviderOperatingHoursRequest Request { get; init; }
}
