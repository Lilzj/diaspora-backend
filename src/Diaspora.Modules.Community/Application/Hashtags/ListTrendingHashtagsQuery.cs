using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Hashtags;

/// <summary>What is trending, optionally narrowed to one country.</summary>
[RequirePolicy(CommunityPolicies.Read)]
public sealed record ListTrendingHashtagsQuery : IQuery<IReadOnlyList<HashtagResponse>>
{
    public string? CountryCode { get; init; }

    public int Limit { get; init; } = 10;
}
