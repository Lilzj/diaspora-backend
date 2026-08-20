using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Discovery;

/// <summary>
/// Searches and browses jobs.
/// <para>
/// Unauthenticated, like the detail read: somebody looking for work sees the market before
/// anybody asks who they are. A signed-in caller additionally gets the "you applied" and
/// "you saved" annotations, which is the only difference.
/// </para>
/// </summary>
public sealed record SearchJobsQuery : IQuery<JobSearchResultResponse>
{
    public required SearchJobsRequest Request { get; init; }

    /// <summary>Whether to compute facet counts. Off by default; a results page asks for them.</summary>
    public bool IncludeFacets { get; init; }
}
