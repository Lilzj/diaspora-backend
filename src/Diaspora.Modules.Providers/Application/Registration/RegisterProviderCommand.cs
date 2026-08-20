using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Registration;

/// <summary>
/// Registers a provider as a draft, owned by its creator.
/// <para>
/// <c>[Transactional]</c> because registration writes an aggregate, allocates a slug and claims
/// media in one logical act; half of it succeeding leaves a provider nobody can find and an
/// asset Media is about to sweep. <c>[Auditable]</c> because a new provider is the single most
/// consequential creation in this module.
/// </para>
/// </summary>
[Transactional]
[Auditable]
public sealed record RegisterProviderCommand : ICommand<ProviderResponse>
{
    public required RegisterProviderRequest Request { get; init; }
}
