using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Hashtags;

/// <summary>Autocomplete for the composer.</summary>
[RequirePolicy(CommunityPolicies.Read)]
public sealed record SuggestHashtagsQuery : IQuery<IReadOnlyList<HashtagResponse>>
{
    public required string Prefix { get; init; }

    public int Limit { get; init; } = 10;
}
