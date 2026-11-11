using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Capabilities;

/// <summary>
/// What this deployment of B7 can do.
/// <para>
/// The call a client makes once at startup so its registration wizard, filter sheet and
/// validation rules come from the server rather than from a shipped constant. The client's
/// provider types, verification kinds and sort options are all deployment decisions today baked
/// into the app; this is what unbakes them.
/// </para>
/// </summary>
[RequirePolicy(ProviderPolicies.Read)]
public sealed record GetProviderCapabilitiesQuery : IQuery<ProviderCapabilitiesResponse>;
