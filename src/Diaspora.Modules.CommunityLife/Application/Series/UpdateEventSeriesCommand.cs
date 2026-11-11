using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Edits a repeating event, at the scope the caller chose.
/// <para>
/// Transactional: one request can rewrite the template and every occurrence still ahead of the
/// clock, and a partial commit would leave half a pattern moved.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.EventEditOwn)]
[Transactional(Reason = "Rewrites the series template and the occurrences within scope.")]
[Auditable]
public sealed record UpdateEventSeriesCommand : ICommand<EventSeriesResponse>
{
    public required Guid SeriesId { get; init; }

    public required UpdateEventSeriesRequest Request { get; init; }
}
