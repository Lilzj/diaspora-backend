using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// The discovery query. The client's Providers tab, and every filter on it.
/// <para>
/// Anonymous callers are served, deliberately: a newcomer who has not signed up should be able
/// to see what is near them, and a directory that demands an account before showing a single
/// restaurant has no way of persuading anybody to make one.
/// </para>
/// </summary>
[RequirePolicy(ProviderPolicies.Read)]
public sealed record SearchProvidersQuery : IQuery<ProviderPageResponse>
{
    public required ProviderSearchRequest Request { get; init; }

    /// <summary>The caller's preferred language, for category translation.</summary>
    public string? LanguageCode { get; init; }
}
