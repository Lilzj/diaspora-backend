using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// One date of a repeating event.
/// <para>
/// Authorised against the occurrence rather than the series, and the difference is real: an
/// occurrence carries its own visibility and its own moderation state, so a single date hidden by a
/// moderator stays hidden even where the pattern it belongs to is public.
/// </para>
/// </summary>
public sealed class GetOccurrenceQueryHandler
    : IQueryHandler<GetOccurrenceQuery, EventOccurrenceResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventSeriesRepository _series;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesReadContext _read;
    private readonly IClock _clock;

    public GetOccurrenceQueryHandler(
        ICommunityEventRepository events,
        IEventSeriesRepository series,
        CommunityLifeAuthorizationService authorization,
        EventSeriesReadContext read,
        IClock clock)
    {
        _events = events;
        _series = series;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<EventOccurrenceResponse>> Handle(
        GetOccurrenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var occurrence = await _events.GetAsync(
            CommunityEventId.From(query.OccurrenceId), cancellationToken);

        if (occurrence?.SeriesId is not { } seriesId)
        {
            return Result.Failure<EventOccurrenceResponse>(
                CommunityLifeErrors.OccurrenceNotFound);
        }

        var readable = await _authorization.RequireEventReadAsync(occurrence, cancellationToken);

        if (readable.IsFailure)
        {
            return Result.Failure<EventOccurrenceResponse>(readable.Error);
        }

        var series = await _series.GetAsync(seriesId, cancellationToken);

        if (series is null)
        {
            return Result.Failure<EventOccurrenceResponse>(
                CommunityLifeErrors.EventSeriesNotFound);
        }

        var next = await _events.FindNextOccurrenceAsync(
            seriesId, _clock.UtcNow, cancellationToken);

        var mapped = await _read.ToOccurrencesAsync(
            series, [occurrence], next?.Id, cancellationToken);

        return Result.Success(mapped[0]);
    }
}
