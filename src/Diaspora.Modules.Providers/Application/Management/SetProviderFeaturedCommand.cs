using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Features a provider, or removes one from the carousel.
/// <para>
/// <b>The aggregate has had <c>SetFeatured</c> since it was written and nothing ever called
/// it.</b> <c>GET /providers/featured</c> has therefore always answered with an empty list — a
/// carousel nobody could fill, on a surface members see. The permission for this
/// (<c>providers:administer</c>) was declared and gated nothing; this is the route it was
/// declared for.
/// </para>
/// </summary>
[Transactional]
[Auditable]
public sealed record SetProviderFeaturedCommand : ICommand<ProviderResponse>
{
    public required Guid ProviderId { get; init; }

    public required SetProviderFeaturedRequest Request { get; init; }
}
