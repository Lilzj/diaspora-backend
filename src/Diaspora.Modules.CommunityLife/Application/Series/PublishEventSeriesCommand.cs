using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Announces a repeating event, and every date already written for it.
/// <para>
/// A separate act from creating one, with its own permission — the same split a single event makes,
/// and for the same reason: publishing puts something in front of strangers.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.EventPublish)]
[Transactional(Reason = "Publishes the series and the occurrences already written for it.")]
[Auditable]
public sealed record PublishEventSeriesCommand : ICommand<EventSeriesResponse>
{
    public required Guid SeriesId { get; init; }
}
