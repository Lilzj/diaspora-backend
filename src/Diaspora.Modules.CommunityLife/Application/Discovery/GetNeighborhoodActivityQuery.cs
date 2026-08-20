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
public sealed record GetNeighborhoodActivityQuery : IQuery<NeighborhoodActivityResponse>
{
    public required Guid AreaId { get; init; }

    /// <summary>How far back to count, in days. Defaults to a year.</summary>
    public int? WindowDays { get; init; }
}
