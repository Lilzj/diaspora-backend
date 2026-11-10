using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// Reads one provider in full, by identifier or by deep-link slug.
/// <para>
/// One query for both, because the two differ in a lookup and agree on everything after —
/// visibility, viewer state, availability and mapping. Two handlers would be two places for the
/// visibility check to drift apart.
/// </para>
/// </summary>
[RequirePolicy(ProviderPolicies.Read)]
public sealed record GetProviderQuery : IQuery<ProviderResponse>
{
    /// <summary>Set exactly one of this and <see cref="Slug"/>.</summary>
    public Guid? ProviderId { get; init; }

    public string? Slug { get; init; }

    public string? LanguageCode { get; init; }
}
