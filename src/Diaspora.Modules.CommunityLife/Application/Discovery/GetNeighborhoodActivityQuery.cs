using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// How much community life there is in one neighbourhood.
/// <para>
/// Anonymous, like every other discovery read: somebody deciding whether to move to an area is
/// exactly the person who has not signed up yet.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record GetNeighborhoodActivityQuery : IQuery<NeighborhoodActivityResponse>
{
    public required Guid AreaId { get; init; }

    /// <summary>How far back to count, in days. Defaults to a year.</summary>
    public int? WindowDays { get; init; }
}
