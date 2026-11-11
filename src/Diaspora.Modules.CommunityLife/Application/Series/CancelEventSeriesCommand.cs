using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Calls a repeating event off, or pauses it.
/// <para>
/// Transactional: cancelling releases every place on every future date, and a partial commit would
/// leave some attendees told and others still expecting to come.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.EventCancel)]
[Transactional(Reason = "Cancels the series and releases every future occurrence's places.")]
[Auditable]
public sealed record CancelEventSeriesCommand : ICommand<EventSeriesResponse>
{
    public required Guid SeriesId { get; init; }

    public required CancelEventSeriesRequest Request { get; init; }
}
