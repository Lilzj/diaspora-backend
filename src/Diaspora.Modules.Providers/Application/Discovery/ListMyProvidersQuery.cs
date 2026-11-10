using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// Every provider the caller holds a role on.
/// <para>
/// <b>Why this exists rather than a filter on discovery.</b> The client wanted "my provider" and
/// reached for <c>GET /providers?ownerId={id}</c>, which answered <b>200 with zero items</b> —
/// <c>ProviderSearchRequest</c> binds no <c>ownerId</c>, and an unbound query parameter is dropped
/// in silence. A filter that looks like it works and does not is worse than one that is absent, and
/// the disclosure sweep recorded it as BG-09.
/// </para>
/// <para>
/// Binding <c>ownerId</c> on the *discovery* route would have been the smaller change and the wrong
/// one: it would let any member enumerate the providers any other member owns, which is a
/// relationship the platform does not otherwise publish. This route answers only for the caller, so
/// the question "which providers are mine" is answerable and "which providers are theirs" stays
/// closed.
/// </para>
/// </summary>
[Authorize(Policy = ProviderPolicies.Read)]
public sealed record ListMyProvidersQuery : IQuery<IReadOnlyList<ProviderSummaryResponse>>
{
    /// <summary>Bounded. This is a picker and a workspace list, not a directory page.</summary>
    public int? Limit { get; init; }

    /// <summary>The caller's preferred language, for category translation.</summary>
    public string? LanguageCode { get; init; }
}
