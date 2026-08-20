using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// What people are searching for in a country's knowledge.
/// <para>
/// Derived from searches rather than from tags. A list built from what has been written tells you
/// what the platform is good at; one built from what people asked for tells you what they need.
/// </para>
/// </summary>
public sealed record ListTrendingTopicsQuery : IQuery<IReadOnlyList<TrendingTopicResponse>>
{
    /// <summary>ISO 3166-1 alpha-2, or absent for the phrases searched without naming a country.</summary>
    public string? CountryCode { get; init; }

    public int? Limit { get; init; }

    /// <summary>
    /// Only the phrases that currently find nothing — the commissioning queue.
    /// <para>
    /// Honoured only for a curator. The list of things this platform cannot answer is exactly the
    /// list a competitor would want, and it is of no use to a reader.
    /// </para>
    /// </summary>
    public bool UnansweredOnly { get; init; }
}
